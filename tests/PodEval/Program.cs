using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProntoPC;

// Pod eval harness: runs the shipping Envelope + GemmaLocalPlanner logic against
// pod/fixtures/eval.json and a set of inline validation checks. Exit 0 = all pass.

static string RepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "pod"))) dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("repo root (with pod/) not found");
}

int failures = 0;
void Check(bool ok, string label)
{
    Console.WriteLine((ok ? "  PASS " : "  FAIL ") + label);
    if (!ok) failures++;
}

string root = RepoRoot();
var env = Envelope.FromJson(File.ReadAllText(Path.Combine(root, "pod/orbs/repair-engine/action-envelope.json")));
Console.WriteLine($"Envelope version {env.Version}\n");

// ── 1. admissibility fixtures ──────────────────────────────────────────────
Console.WriteLine("== AdmissibleSet fixtures ==");
var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "pod/fixtures/eval.json"))).RootElement;
foreach (var sc in fixtures.GetProperty("scenarios").EnumerateArray())
{
    string name = sc.GetProperty("name").GetString() ?? "?";
    var admissible = env.AdmissibleSet(sc.GetProperty("diagnostics"));
    var missing = sc.GetProperty("includes").EnumerateArray().Select(x => x.GetString()!).Where(id => !admissible.Contains(id)).ToList();
    var leaked = sc.GetProperty("excludes").EnumerateArray().Select(x => x.GetString()!).Where(id => admissible.Contains(id)).ToList();
    Check(missing.Count == 0, $"{name}: includes present" + (missing.Count > 0 ? " — MISSING " + string.Join(",", missing) : ""));
    Check(leaked.Count == 0, $"{name}: excludes absent" + (leaked.Count > 0 ? " — LEAKED " + string.Join(",", leaked) : ""));
}

// ── 2. Validate: clamp / risk-stamp / subsumption / drops ──────────────────
Console.WriteLine("\n== Validate ==");
var messy = JsonDocument.Parse(@"{
  ""temp_files"":[{""size_mb"":500}], ""browser_caches"":[{""browser"":""Chrome"",""size_mb"":300}],
  ""update_leftovers"":{""windows_old_present"":true,""windows_old_gb"":8,""windows_update_cache_mb"":0,""delivery_optimization_mb"":0},
  ""memory"":{""percent_used"":50}, ""network"":{""connectivity"":{""internet_reachable"":true}}, ""dns_cache"":{""entries"":0}
}").RootElement;
var authored = JsonDocument.Parse(@"{""summary"":""s"",""actions"":[
  {""id"":""clear_temp_files"",""friendly_name"":""T"",""risk"":""low"",""estimated_mb_recovered"":5},
  {""id"":""remove_windows_old"",""friendly_name"":""W"",""risk"":""low"",""estimated_mb_recovered"":99999},
  {""id"":""clear_all_browser_caches"",""friendly_name"":""B"",""risk"":""low"",""estimated_mb_recovered"":300},
  {""id"":""clear_chrome_cache"",""friendly_name"":""C"",""risk"":""low"",""estimated_mb_recovered"":300},
  {""id"":""reset_network_stack"",""friendly_name"":""N"",""risk"":""low"",""estimated_mb_recovered"":0},
  {""id"":""frobnicate"",""friendly_name"":""X"",""risk"":""low"",""estimated_mb_recovered"":0}
]}").RootElement;
var (plan, dropped) = env.Validate(authored, messy);
var kept = plan["actions"]!.AsArray().ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!);
Check(kept.ContainsKey("clear_temp_files") && kept["clear_temp_files"]!["estimated_mb_recovered"]!.GetValue<int>() == 500, "clear_temp_files mb set to true measure 5 -> 500 (under-claim corrected up)");
Check(kept.ContainsKey("remove_windows_old") && kept["remove_windows_old"]!["risk"]!.GetValue<string>() == "medium", "remove_windows_old risk stamped low -> medium");
Check(kept.ContainsKey("remove_windows_old") && kept["remove_windows_old"]!["estimated_mb_recovered"]!.GetValue<int>() == 8192, "remove_windows_old mb clamped 99999 -> 8192 (gb->mb)");
Check(!kept.ContainsKey("reset_network_stack") && dropped.Any(d => d.StartsWith("reset_network_stack")), "reset_network_stack dropped (false precondition)");
Check(!kept.ContainsKey("frobnicate") && dropped.Any(d => d.StartsWith("frobnicate")), "frobnicate dropped (not in catalogue)");
Check(!kept.ContainsKey("clear_chrome_cache") && dropped.Any(d => d.Contains("subsumed")), "clear_chrome_cache dropped (subsumed by umbrella)");

// ── 2b. Completeness backstop: surface measured reclaims the model omitted ──
Console.WriteLine("\n== Validate: completeness backstop ==");
// recycle bin (1024 MB) and browser caches (300 MB) are measured + admissible, but the
// model authors neither umbrella — it only names temp files and a bare per-browser member.
var bDiag = JsonDocument.Parse(@"{
  ""temp_files"":[{""size_mb"":200}], ""recycle_bin"":{""size_mb"":1024},
  ""browser_caches"":[{""browser"":""Chrome"",""size_mb"":300}],
  ""update_leftovers"":{""windows_old_present"":false,""windows_old_gb"":0,""windows_update_cache_mb"":0,""delivery_optimization_mb"":0},
  ""memory"":{""percent_used"":50}, ""network"":{""connectivity"":{""internet_reachable"":true}}, ""dns_cache"":{""entries"":0}
}").RootElement;
var bAuthored = JsonDocument.Parse(@"{""summary"":""s"",""actions"":[
  {""id"":""clear_temp_files"",""friendly_name"":""Temp"",""risk"":""low"",""estimated_mb_recovered"":200},
  {""id"":""clear_chrome_cache"",""friendly_name"":""Chrome"",""risk"":""low"",""estimated_mb_recovered"":0}
]}").RootElement;
var (bPlan, bDropped) = env.Validate(bAuthored, bDiag);
var bKept = bPlan["actions"]!.AsArray().ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!);
var bAdded = (bPlan["completeness_added"] as JsonArray)?.Select(x => x!.GetValue<string>()).ToHashSet() ?? new HashSet<string>();
Check(bKept.ContainsKey("empty_recycle_bin") && bKept["empty_recycle_bin"]!["estimated_mb_recovered"]!.GetValue<int>() == 1024, "empty_recycle_bin ADDED with measured 1024 MB (model omitted it)");
Check(bKept.ContainsKey("empty_recycle_bin") && bKept["empty_recycle_bin"]!["friendly_name"]!.GetValue<string>() == "Empty the Recycle Bin" && bKept["empty_recycle_bin"]!["risk"]!.GetValue<string>() == "low", "added action carries plain-English label + envelope risk");
Check(bAdded.Contains("empty_recycle_bin") && bAdded.Contains("clear_all_browser_caches"), "completeness_added lists both measured omissions");
Check(bKept.ContainsKey("clear_all_browser_caches") && bKept["clear_all_browser_caches"]!["estimated_mb_recovered"]!.GetValue<int>() == 300, "added umbrella carries measured 300 MB");
Check(!bKept.ContainsKey("clear_chrome_cache") && bDropped.Any(d => d.Contains("subsumed")), "added umbrella subsumes the model's bare member (no double-count)");
Check(!bAdded.Contains("clear_temp_files"), "model-authored measured action is NOT re-added");

// ── 3. GemmaLocalPlanner author path (fake inference) ──────────────────────
Console.WriteLine("\n== GemmaLocalPlanner (fake inference) ==");
// The author receives ONLY a completed, validated System Audit (TOTEM-X-0074) — never
// raw diagnostics. Build a complete audit from the messy set (all 25 sections present).
var messyDiag = messy.Deserialize<Dictionary<string, object?>>()!;
foreach (var k in SystemAuditor.ExpectedKeys) if (!messyDiag.ContainsKey(k)) messyDiag[k] = new Dictionary<string, object?>();
var messyAudit = SystemAuditor.Assess(messyDiag);
Check(messyAudit.IsValid, "assembled diagnostics form a complete, valid audit");
var admissibleMessy = env.AdmissibleSet(messyAudit.Diagnostics);
var planner = new GemmaLocalPlanner(new FakeInference());
var result = await planner.AuthorAsync(messyAudit, admissibleMessy, CancellationToken.None);
Check(result != null && result.Provider == "local-model", "author returns provider=local-model");
Check(result != null && result.Plan.TryGetProperty("actions", out _), "author parsed prose-wrapped JSON into a plan");
if (result != null)
{
    var (vp, _) = env.Validate(result.Plan, messyAudit.Diagnostics);
    Check(vp["actions"]!.AsArray().Count > 0, "authored plan survives envelope validation");
}

// ── 4. System Audit precondition (TOTEM-X-0074) ─────────────────────────────
// The application performs the audit; the model may author a plan ONLY from a completed,
// validated audit. An invalid/incomplete/absent audit reaches NO model invocation.
Console.WriteLine("\n== System Audit precondition ==");

// A complete diagnostic set (all 25 sections attempted) validates into an audit.
var fullDiag = new Dictionary<string, object?>();
foreach (var k in SystemAuditor.ExpectedKeys) fullDiag[k] = new Dictionary<string, object?>();
var goodAudit = SystemAuditor.Assess(fullDiag);
Check(goodAudit.IsValid && goodAudit.SectionsPresent == SystemAuditor.ExpectedCount, "complete 25/25 diagnostics -> VALID audit");
Check(goodAudit.EvidenceHash.Length == 64, "valid audit carries a SHA-256 evidence hash");

// One missing section -> INVALID (incomplete_audit).
var shortDiag = new Dictionary<string, object?>(fullDiag);
shortDiag.Remove(SystemAuditor.ExpectedKeys[0]);
var shortAudit = SystemAuditor.Assess(shortDiag);
Check(!shortAudit.IsValid && shortAudit.InvalidReason == "incomplete_audit", "missing a section -> INVALID (incomplete_audit)");

// The explicit failure factory is invalid and carries no evidence.
var failedAudit = SystemAudit.Failed("audit_did_not_complete");
Check(!failedAudit.IsValid && failedAudit.EvidenceHash.Length == 0, "SystemAudit.Failed() is invalid with no evidence");

// Zero model invocations for an invalid/incomplete/absent audit; the model IS invoked
// exactly once for a valid audit — a counting fake proves both.
var counter = new CountingInference();
var gated = new GemmaLocalPlanner(counter);
var admissibleForCount = env.AdmissibleSet(messyAudit.Diagnostics);   // non-empty admissible set
var rFailed = await gated.AuthorAsync(failedAudit, admissibleForCount, CancellationToken.None);
Check(rFailed == null && counter.Calls == 0, "FAILED audit -> author declines with ZERO model invocations");
Check(((IPlanAuthorDiagnostics)gated).LastDeclineReason == "system_audit_invalid", "decline reason == system_audit_invalid");
var rShort = await gated.AuthorAsync(shortAudit, admissibleForCount, CancellationToken.None);
Check(rShort == null && counter.Calls == 0, "INCOMPLETE audit -> author declines, still ZERO model invocations");
var rGood = await gated.AuthorAsync(messyAudit, admissibleForCount, CancellationToken.None);
Check(rGood != null && rGood.Provider == "local-model", "VALID audit -> author proceeds to the local model");
Check(counter.Calls == 1, "valid audit invoked the model exactly once");

Console.WriteLine($"\n{(failures == 0 ? "ALL PASS" : failures + " FAILURE(S)")}");
Environment.Exit(failures == 0 ? 0 : 1);

// Returns prose-wrapped JSON including an inadmissible action, to prove parse + validation.
sealed class FakeInference : ILocalInference
{
    public bool Available => true;
    public string ModelName => "fake";
    public string? Complete(string prompt, string schema, CancellationToken ct) =>
        "Sure!\n{\"summary\":\"500 MB temp, 8 GB Windows.old.\",\"actions\":[" +
        "{\"id\":\"clear_temp_files\",\"friendly_name\":\"Temp\",\"category\":\"cleanup\",\"estimated_mb_recovered\":500}," +
        "{\"id\":\"reset_network_stack\",\"friendly_name\":\"Net\",\"category\":\"network\",\"estimated_mb_recovered\":0}]}";
    // Multi-turn surface (TOTEM-0026): the plan-authoring eval never calls it, but the
    // fake must satisfy the interface. Echo the last user turn so it stays deterministic.
    public string? Chat(System.Collections.Generic.IReadOnlyList<ChatTurn> history, CancellationToken ct) =>
        history.Count == 0 ? null : history[history.Count - 1].Text;
}

// Counts model invocations so the System Audit precondition (TOTEM-X-0074) can prove
// ZERO Complete() calls on an invalid/incomplete audit and exactly one on a valid audit.
sealed class CountingInference : ILocalInference
{
    public int Calls;
    public bool Available => true;
    public string ModelName => "counting";
    public string? Complete(string prompt, string schema, CancellationToken ct)
    {
        Calls++;
        return "{\"summary\":\"ok\",\"actions\":[{\"id\":\"clear_temp_files\",\"friendly_name\":\"Temp\",\"category\":\"cleanup\",\"estimated_mb_recovered\":500}]}";
    }
    public string? Chat(System.Collections.Generic.IReadOnlyList<ChatTurn> history, CancellationToken ct) => null;
}
