using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProntoPC;
using ProntoPC.Governance;

// Proves the bounded Technician->Communications relay INTEGRATION against the REAL app types,
// closing the TOTEM-X-0078 audit gaps: (#1) findings publish only from a canonical VALID SystemAudit
// and carry its EvidenceHash; (#2) plan actions are envelope-validated (uncatalogued id never
// relays); (#4) reads go through a session-boundary authorization. Engine-free (PodEval include set +
// Governance + the integration); no model, no gate, no Orchestrator.

int failures = 0;
void Check(bool c, string label) { Console.WriteLine((c ? "PASS " : "FAIL ") + label); if (!c) failures++; }

static string RepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "pod"))) d = d.Parent;
    return d?.FullName ?? throw new DirectoryNotFoundException("repo root (with pod/) not found");
}

string root = RepoRoot();
var envelope = Envelope.FromJson(File.ReadAllText(Path.Combine(root, "pod", "orbs", "repair-engine", "action-envelope.json")));

// A rich machine so real cleanup actions are admissible (mirrors the ModelSmoke fixture).
var diagDoc = JsonDocument.Parse(@"{
 ""os"":{""edition"":""Windows 11 Home"",""build"":""22631"",""hostname"":""RELAY-PC""},
 ""memory"":{""used_gb"":13,""total_gb"":16,""percent_used"":82},
 ""disk"":[{""device"":""C:"",""percent_used"":91,""free_gb"":22,""total_gb"":256}],
 ""temp_files"":[{""size_mb"":740}], ""browser_caches"":[{""browser"":""Chrome"",""size_mb"":610}],
 ""recycle_bin"":{""size_mb"":1300},
 ""update_leftovers"":{""windows_old_present"":true,""windows_old_gb"":9,""windows_update_cache_mb"":0,""delivery_optimization_mb"":0},
 ""network"":{""connectivity"":{""internet_reachable"":true}},""dns_cache"":{""entries"":0},""bsod_history"":[],""device_errors"":[],""startup_programs"":[]
}");
JsonElement diag = diagDoc.RootElement;

// A valid audit (the internal ctor is visible because SystemAudit.cs is compiled into this eval).
const string EvidenceHash = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2";
var validAudit = new SystemAudit("1", DateTime.UtcNow, diag, 25, 25, 25, EvidenceHash, true, "");
var failedAudit = SystemAudit.Failed("audit did not complete");

// A plan with one real cleanup id + one uncatalogued id that MUST be dropped before relay.
var planDoc = JsonDocument.Parse(@"{
 ""summary"":""C: is 91% full; clearing temporary files can recover space."",
 ""actions"":[ {""id"":""clear_temp_files""}, {""id"":""definitely_not_a_real_action""} ]
}");
JsonElement authoredPlan = planDoc.RootElement;

string temp = Path.Combine(Path.GetTempPath(), "prontopc-relay-int-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var clock = (Func<DateTimeOffset>)(() => new DateTimeOffset(2026, 9, 3, 0, 15, 0, TimeSpan.Zero));
    var relay = new AppendOnlyFindingsRelay(Path.Combine(temp, "relay.jsonl"), clock);
    var integration = new FindingsRelayIntegration(relay, envelope);

    // gap #1 — an invalid audit relays nothing.
    Check(integration.PublishFindings("s1", failedAudit, authoredPlan, "local-model", "gemma-3-1b-it") is null,
        "gap#1: an INVALID SystemAudit publishes nothing");

    // gap #1 — a valid audit publishes and binds DiagnosticsSha256 to the audit's own EvidenceHash.
    var rec = integration.PublishFindings("s1", validAudit, authoredPlan, "local-model", "gemma-3-1b-it");
    Check(rec is not null, "gap#1: a VALID SystemAudit publishes a findings record");
    Check(rec!.DiagnosticsSha256 == EvidenceHash, "gap#1: findings are bound to the audit's canonical EvidenceHash (not an opaque string)");
    Check(rec.PlanSummary.Contains("91% full"), "the plan summary relays through");

    // gap #2 — the uncatalogued id is dropped by envelope validation before it can relay.
    Check(rec.Actions.All(a => a.Id != "definitely_not_a_real_action"), "gap#2: an uncatalogued action id never relays");
    bool tempAdmissible = envelope.AdmissibleSet(diag).Contains("clear_temp_files");
    Console.WriteLine("  (info) clear_temp_files admissible on this fixture: " + tempAdmissible
        + "; relayed actions: " + string.Join(",", rec.Actions.Select(a => a.Id)));
    if (tempAdmissible)
        Check(rec.Actions.Any(a => a.Id == "clear_temp_files"), "gap#2: a catalogued+admissible action is kept and relayed");

    // gap #4 — session-boundary authorization: a caller reads ONLY the session it holds.
    integration.PublishFindings("s2", validAudit, authoredPlan, "local-model", "gemma-3-1b-it");
    var mine = integration.ReadAuthorizedFindings("s1", "s1");
    Check(mine is not null && mine.RecordId == rec.RecordId, "gap#4: a caller reads the latest findings for the session it holds");
    Check(integration.ReadAuthorizedFindings("s1", "s2") is null, "gap#4: a caller CANNOT read another session's findings");
    Check(integration.ReadAuthorizedFindings("", "s1") is null, "gap#4: an empty held session authorizes nothing");
}
finally
{
    if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
    diagDoc.Dispose(); planDoc.Dispose();
}

Console.WriteLine(failures == 0 ? "\nFINDINGS RELAY INTEGRATION: ALL CHECKS PASS" : $"\n{failures} FAILURE(S)");
Environment.Exit(failures == 0 ? 0 : 1);
