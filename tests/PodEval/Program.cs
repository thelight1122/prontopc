using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
  {""id"":""clear_temp_files"",""friendly_name"":""T"",""risk"":""low"",""estimated_mb_recovered"":999999},
  {""id"":""remove_windows_old"",""friendly_name"":""W"",""risk"":""low"",""estimated_mb_recovered"":99999},
  {""id"":""clear_all_browser_caches"",""friendly_name"":""B"",""risk"":""low"",""estimated_mb_recovered"":300},
  {""id"":""clear_chrome_cache"",""friendly_name"":""C"",""risk"":""low"",""estimated_mb_recovered"":300},
  {""id"":""reset_network_stack"",""friendly_name"":""N"",""risk"":""low"",""estimated_mb_recovered"":0},
  {""id"":""frobnicate"",""friendly_name"":""X"",""risk"":""low"",""estimated_mb_recovered"":0}
]}").RootElement;
var (plan, dropped) = env.Validate(authored, messy);
var kept = plan["actions"]!.AsArray().ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!);
Check(kept.ContainsKey("clear_temp_files") && kept["clear_temp_files"]!["estimated_mb_recovered"]!.GetValue<int>() == 500, "clear_temp_files mb clamped 999999 -> 500");
Check(kept.ContainsKey("remove_windows_old") && kept["remove_windows_old"]!["risk"]!.GetValue<string>() == "medium", "remove_windows_old risk stamped low -> medium");
Check(kept.ContainsKey("remove_windows_old") && kept["remove_windows_old"]!["estimated_mb_recovered"]!.GetValue<int>() == 8192, "remove_windows_old mb clamped 99999 -> 8192 (gb->mb)");
Check(!kept.ContainsKey("reset_network_stack") && dropped.Any(d => d.StartsWith("reset_network_stack")), "reset_network_stack dropped (false precondition)");
Check(!kept.ContainsKey("frobnicate") && dropped.Any(d => d.StartsWith("frobnicate")), "frobnicate dropped (not in catalogue)");
Check(!kept.ContainsKey("clear_chrome_cache") && dropped.Any(d => d.Contains("subsumed")), "clear_chrome_cache dropped (subsumed by umbrella)");

// ── 3. GemmaLocalPlanner author path (fake inference) ──────────────────────
Console.WriteLine("\n== GemmaLocalPlanner (fake inference) ==");
var admissibleMessy = env.AdmissibleSet(messy);
var planner = new GemmaLocalPlanner(new FakeInference());
var result = await planner.AuthorAsync(messy, admissibleMessy, CancellationToken.None);
Check(result != null && result.Provider == "local-model", "author returns provider=local-model");
Check(result != null && result.Plan.TryGetProperty("actions", out _), "author parsed prose-wrapped JSON into a plan");
if (result != null)
{
    var (vp, _) = env.Validate(result.Plan, messy);
    Check(vp["actions"]!.AsArray().Count > 0, "authored plan survives envelope validation");
}

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
}
