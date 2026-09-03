using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

// The Pod processor, client side. Owns phase/session flow ONLY — no diagnostic or
// repair logic. It HOLDS the Action Envelope and KEYS the repair-plan Umbilical to
// it: it compiles the per-machine admissible set, asks the attached plan authors (in
// provider order) for a plan, and VALIDATES every authored plan against the envelope
// before it can reach consent. See pod/orchestrator/SPEC.md.
//
// The desktop author is the bundled local-model Satellite. It remains on-device;
// payment/session recording is a separate, post-validation operation.

// PlanResult + IPlanAuthor live in PlanContracts.cs (kept engine-free so the Pod eval
// can compile the plan logic without the Windows-only repair engine).

// Outcome the Orchestrator hands the view: a validated local plan, or an honest local
// decline reason. No cloud author is consulted from this path.
sealed class PlanOutcome
{
    public bool IsOk { get; private init; }
    public JsonObject? Plan { get; private init; }
    public string Provider { get; private init; } = "";
    public string Model { get; private init; } = "";
    public IReadOnlyList<string> Dropped { get; private init; } = Array.Empty<string>();
    public string DeclineReason { get; private init; } = "";

    public static PlanOutcome Ok(JsonObject plan, string provider, string model, List<string> dropped) =>
        new() { IsOk = true, Plan = plan, Provider = provider, Model = model, Dropped = dropped };
    public static PlanOutcome Unavailable(string reason) => new() { IsOk = false, DeclineReason = reason };
}

sealed class Orchestrator
{
    // The version this client build is keyed to (the Umbilical envelope_ref.version).
    const string KeyedEnvelopeVersion = "0.1.0";

    // On-device generation is occasionally flaky — a decode can come back empty, unparseable,
    // or (after envelope validation) with no admissible actions, yet the next attempt succeeds.
    // Retry the local author this many times on TRANSIENT declines before make-good.
    const int MaxAuthorAttempts = 3;

    // Declines that a fresh attempt can plausibly fix. Deterministic declines (no model,
    // no admissible actions) are NOT listed — retrying them just wastes the customer's time.
    static bool IsTransientDecline(string reason) => reason is
        "local_completion_empty" or "local_inference_failed" or "local_plan_json_invalid"
        or "local_author_exception" or "local_plan_rejected_by_envelope";

    readonly Envelope? _env;
    readonly IReadOnlyList<IPlanAuthor> _authors;

    public Orchestrator(IEnumerable<IPlanAuthor> authorsInProviderOrder)
    {
        // Key the Umbilical: a client built for 0.1.0 must not run a newer envelope it
        // doesn't understand. On load failure or version mismatch, keep _env null — the
        // plan phase then degrades to a labelled offline plan, but the repair/verify
        // phases (which don't touch the envelope) are unaffected, so the app still works.
        Envelope? env = null;
        try { var e = Envelope.Load(); env = e.Version == KeyedEnvelopeVersion ? e : null; }
        catch { env = null; }
        _env = env;
        _authors = authorsInProviderOrder.ToList();
    }

    // Plan phase: require a COMPLETED, VALIDATED System Audit (TOTEM-X-0074) -> local
    // author -> validate against the keyed envelope -> accept or report the local
    // decline. The public plan-authoring path accepts only the audit report, never raw
    // diagnostics; an invalid/incomplete audit reaches no author and no model invocation.
    // The authors collection is retained for the engine-free seam, but this desktop
    // supplies exactly one local author.
    public async Task<PlanOutcome> AuthorPlanAsync(SystemAudit audit, CancellationToken ct = default)
    {
        // Precondition gate: no model, findings, consent, or repair path is reachable
        // without a valid audit. This runs before any author is consulted.
        if (!audit.IsValid)
            return PlanOutcome.Unavailable("system_audit_" +
                (string.IsNullOrEmpty(audit.InvalidReason) ? "invalid" : audit.InvalidReason));

        if (_env is null) return PlanOutcome.Unavailable("action_envelope_unavailable");
        var diagEl = audit.Diagnostics;
        var admissible = _env.AdmissibleSet(diagEl);

        foreach (var author in _authors)
        {
            string lastDecline = "local_author_declined";
            for (int attempt = 1; attempt <= MaxAuthorAttempts; attempt++)
            {
                PlanResult? r;
                try { r = await author.AuthorAsync(audit, admissible, ct); }
                catch (OperationCanceledException) { return PlanOutcome.Unavailable("local_inference_cancelled"); }
                catch { r = null; lastDecline = "local_author_exception"; }

                if (r is not null)
                {
                    var (plan, dropped) = _env.Validate(r.Plan, diagEl);
                    if (ActionCount(plan) > 0) return PlanOutcome.Ok(plan, r.Provider, r.Model, dropped);
                    lastDecline = "local_plan_rejected_by_envelope";   // model replied but no admissible action survived
                }
                else
                {
                    lastDecline = (author as IPlanAuthorDiagnostics)?.LastDeclineReason ?? lastDecline;
                    if (!IsTransientDecline(lastDecline)) break;       // deterministic — a retry cannot change it
                }

                // Brief settle before the next attempt (lets a warm-up / transient inference error clear).
                if (attempt < MaxAuthorAttempts)
                {
                    try { await Task.Delay(200, ct); }
                    catch (OperationCanceledException) { return PlanOutcome.Unavailable("local_inference_cancelled"); }
                }
            }
            return PlanOutcome.Unavailable(lastDecline);
        }
        return PlanOutcome.Unavailable("local_author_not_configured");
    }

    // Repair + verify phases: invoke the repair-engine Orb over the approved actions,
    // reporting progress, then summarize (freed space, restart-needed). The Orchestrator
    // sequences and reports; the engine (Repair) owns what each action does. Envelope-
    // independent — the plan that produced `approved` was already validated at consent.
    public async Task<RepairOutcome> RunRepairAsync(
        IReadOnlyList<Dictionary<string, object?>> approved,
        IProgress<RepairProgress>? progress,
        CancellationToken ct = default)
    {
        var results = new List<Dictionary<string, object?>>();
        int total = approved.Count, i = 0;
        foreach (var action in approved)
        {
            i++;
            string name = action.GetValueOrDefault("friendly_name")?.ToString() ?? "";
            progress?.Report(new RepairProgress(i, total, name));
            var r = await Task.Run(() => Repair.Execute(action), ct);
            results.Add(r);
        }

        int freed = results.Sum(r => Mb(r.GetValueOrDefault("mb_freed")));
        int done = results.Count(r => (r.GetValueOrDefault("status")?.ToString()) == "done");
        bool restart = Repair.NeedsRestart(results);
        string msg = freed > 0 ? $"{done} repairs completed · {FormatMb(freed)} recovered."
                               : $"{done} items completed.";
        if (restart) msg += "\n\nA restart is recommended to finish applying some repairs.";
        return new RepairOutcome(results, done, freed, restart, msg);
    }

    static int ActionCount(JsonObject plan) =>
        plan.TryGetPropertyValue("actions", out var a) && a is JsonArray arr ? arr.Count : 0;
    static int Mb(object? v) { int.TryParse(v?.ToString(), out int m); return m; }
    static string FormatMb(int mb) => mb >= 1024 ? $"{mb / 1024.0:0.0} GB" : $"{mb} MB";
}

// Repair-phase progress + outcome (the "repair" and "verify" phases).
sealed record RepairProgress(int Index, int Total, string Name);
sealed record RepairOutcome(
    List<Dictionary<string, object?>> Results, int Done, int FreedMb, bool NeedsRestart, string Message);
