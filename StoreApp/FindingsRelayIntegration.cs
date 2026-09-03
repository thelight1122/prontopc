using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProntoPC.Governance;

namespace ProntoPC;

// ── Technician → Communications relay INTEGRATION ────────────────────────────────────────────
// Binds the pure AppendOnlyFindingsRelay (Governance/) to the app's real, validated types, closing
// the gaps the independent audit (TOTEM-X-0078) recorded against the raw component:
//
//   gap #1  findings are bound to a canonical VALIDATED SystemAudit — publish is refused unless the
//           audit is valid, and the relay's DiagnosticsSha256 carries the audit's OWN EvidenceHash
//           (which only a completed, validated audit can mint), not an opaque caller string;
//   gap #2  the plan's action ids are re-validated against the Action Envelope catalogue (on this
//           machine's diagnostics) before publish — an uncatalogued/inadmissible id never relays;
//   gap #4  reads go through a session-boundary AUTHORIZATION — a caller may read only the session it
//           holds, not any session string (this is authorization, not raw filtering).
//
// gap #3 (tail-truncation / whole-file replacement) is closed by the relay's separate-failure-domain
// head anchor; gap #5 (the live runtime path) is these caller methods, which the Technician Orb
// (after AuthorPlanAsync) and the Communications Orb call. This opens no gate — PipelineGovernance
// stays enabled:false — and executes nothing (Manifest I2: actionable intent is a consent handoff).
internal sealed class FindingsRelayIntegration
{
    readonly AppendOnlyFindingsRelay _relay;
    readonly Envelope _envelope;

    internal FindingsRelayIntegration(AppendOnlyFindingsRelay relay, Envelope envelope)
    {
        _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }

    // The Technician Orb publishes its findings. Returns null (nothing relayed) unless the audit is a
    // canonical VALID SystemAudit. The published actions are exactly those the Action Envelope keeps
    // for this machine; DiagnosticsSha256 is the audit's own EvidenceHash, binding the relay to that
    // exact validated scan.
    internal FindingsRelayRecord? PublishFindings(string sessionId, SystemAudit audit, JsonElement authoredPlan,
        string provider, string model)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        if (audit is null || !audit.IsValid) return null;                 // gap #1: no valid audit → no relay

        var (validated, _) = _envelope.Validate(authoredPlan, audit.Diagnostics); // gap #2: envelope-validate
        string summary = validated["summary"]?.GetValue<string>() ?? "";
        FindingsAction[] actions = ExtractActions(validated);

        return _relay.Append(sessionId, provider ?? "", model ?? "", summary, actions, audit.EvidenceHash);
    }

    // The Communications Orb reads through a session boundary. `heldSessionId` is the session the
    // caller is authorized for (the token it arrived with); it may read findings ONLY for that
    // session. Any mismatch returns null — a caller cannot read another session's findings.
    internal FindingsRelayRecord? ReadAuthorizedFindings(string heldSessionId, string requestedSessionId)
    {
        if (string.IsNullOrEmpty(heldSessionId)) return null;
        if (!string.Equals(heldSessionId, requestedSessionId, StringComparison.Ordinal)) return null; // gap #4
        return _relay.ReadLatestForSession(heldSessionId);
    }

    static FindingsAction[] ExtractActions(JsonObject plan)
    {
        var list = new List<FindingsAction>();
        if (plan.TryGetPropertyValue("actions", out var node) && node is JsonArray actions)
        {
            foreach (var a in actions)
            {
                if (a is not JsonObject act) continue;
                string id = act["id"]?.GetValue<string>() ?? "";
                if (id.Length == 0) continue;
                string title = act["title"]?.GetValue<string>()
                            ?? act["label"]?.GetValue<string>()
                            ?? act["summary"]?.GetValue<string>()
                            ?? id;
                int mb = 0;
                if (act.TryGetPropertyValue("estimated_mb_recovered", out var m) && m is not null)
                    { try { mb = m.GetValue<int>(); } catch { mb = 0; } }
                list.Add(new FindingsAction(id, title, mb));
            }
        }
        return list.ToArray();
    }
}
