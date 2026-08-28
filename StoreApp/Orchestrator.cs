using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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
// Stage 1: the only author is the cloud broker. Stage 2 prepends the Gemma
// local-model Satellite as the first author (offline, no billing cliff).

// Result of one author attempt. null return from an author => fall through to the next.
sealed record PlanResult(JsonElement Plan, string Provider, string Model);

interface IPlanAuthor
{
    // Author a plan; the model may only choose from `admissible`. Return null to
    // decline (unavailable/unreachable) so the Orchestrator tries the next author.
    Task<PlanResult?> AuthorAsync(JsonElement diag, IReadOnlySet<string> admissible, CancellationToken ct);
}

// Outcome the Orchestrator hands the view: a validated plan, or honest unavailable.
sealed class PlanOutcome
{
    public bool IsOk { get; private init; }
    public JsonObject? Plan { get; private init; }
    public string Provider { get; private init; } = "";
    public string Model { get; private init; } = "";
    public IReadOnlyList<string> Dropped { get; private init; } = Array.Empty<string>();

    public static PlanOutcome Ok(JsonObject plan, string provider, string model, List<string> dropped) =>
        new() { IsOk = true, Plan = plan, Provider = provider, Model = model, Dropped = dropped };
    public static PlanOutcome Unavailable() => new() { IsOk = false };
}

sealed class Orchestrator
{
    // The version this client build is keyed to (the Umbilical envelope_ref.version).
    const string KeyedEnvelopeVersion = "0.1.0";

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

    // Plan phase: author -> validate against the keyed envelope -> accept or fall through.
    public async Task<PlanOutcome> AuthorPlanAsync(Dictionary<string, object?> diag, CancellationToken ct = default)
    {
        if (_env is null) return PlanOutcome.Unavailable();   // unkeyed -> offline plan
        var diagEl = JsonSerializer.SerializeToElement(diag);
        var admissible = _env.AdmissibleSet(diagEl);

        foreach (var author in _authors)
        {
            PlanResult? r;
            try { r = await author.AuthorAsync(diagEl, admissible, ct); }
            catch { r = null; }
            if (r is null) continue;

            var (plan, dropped) = _env.Validate(r.Plan, diagEl);
            if (ActionCount(plan) == 0) continue;   // gutted by validation -> next author
            return PlanOutcome.Ok(plan, r.Provider, r.Model, dropped);
        }
        return PlanOutcome.Unavailable();
    }

    // Envelope pass-through so the view can also sanitize the labelled offline plan
    // (it is not the AI plan, but it must still be admissible and honestly measured).
    // If the envelope is unkeyed, pass the plan through unchanged.
    public JsonObject Sanitize(JsonElement plan, Dictionary<string, object?> diag)
    {
        if (_env is null) return JsonNode.Parse(plan.GetRawText())!.AsObject();
        var (sane, _) = _env.Validate(plan, JsonSerializer.SerializeToElement(diag));
        return sane;
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

// ── the Stage 1 author: the cloud broker (submit-diagnostics) ──────────────────
sealed class CloudPlanAuthor : IPlanAuthor
{
    readonly string _baseUrl;
    readonly Func<string> _token;

    public CloudPlanAuthor(string baseUrl, Func<string> token) { _baseUrl = baseUrl; _token = token; }

    public async Task<PlanResult?> AuthorAsync(JsonElement diag, IReadOnlySet<string> admissible, CancellationToken ct)
    {
        // The cloud broker enforces its own catalogue; the Orchestrator re-validates
        // the result against the envelope, so `admissible` is not sent here.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        string payload = JsonSerializer.Serialize(new { token = _token(), diagnostics = diag });
        var resp = await http.PostAsync($"{_baseUrl}/api/submit-diagnostics",
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        string body = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        string status = root.TryGetProperty("status", out var st) ? (st.GetString() ?? "") : "";

        if (status == "unavailable") return null;

        JsonElement plan = default;
        bool have = (status == "ok" && root.TryGetProperty("plan", out plan))
                 || (status == "" && resp.IsSuccessStatusCode && root.TryGetProperty("plan", out plan));
        if (!have) return null;

        string provider = "cloud", model = "";
        if (root.TryGetProperty("provenance", out var prov))
        {
            provider = prov.TryGetProperty("provider", out var pv) ? pv.GetString() ?? "cloud" : "cloud";
            model = prov.TryGetProperty("model", out var mv) ? mv.GetString() ?? "" : "";
        }
        return new PlanResult(plan.Clone(), provider, model);
    }
}
