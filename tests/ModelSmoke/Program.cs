using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ProntoPC;

// On-device Satellite smoke test. Exit 0 = pass or skip; exit 1 = failure.
static string RepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "pod"))) d = d.Parent;
    return d?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
}

string root = RepoRoot();
string modelDir = Path.Combine(root, "StoreApp", "Assets", "models", ProductModel.Name);
for (int i = 0; i < args.Length - 1; i++)
    if (args[i].Equals("--model-dir", StringComparison.OrdinalIgnoreCase)) modelDir = Path.GetFullPath(args[i + 1]);
string modelName = Path.GetFileName(Path.TrimEndingDirectorySeparator(modelDir));
var inf = OnnxGenAiInference.CreateOrAbsent(modelDir, modelName);
if (!inf.Available)
{
    Console.WriteLine("SKIP: model not bundled at " + modelDir + " (build it via the Satellite SPEC).");
    return;
}

var env = Envelope.FromJson(File.ReadAllText(Path.Combine(root, "pod/orbs/repair-engine/action-envelope.json")));

// The System Audit precondition (TOTEM-X-0074): the on-device author receives ONLY a
// completed, validated audit — never raw diagnostics. Build the diagnostic set, complete
// every section the real Diagnostics.Collect() always attempts, then Assess() into an
// immutable audit and drive the author through it exactly as the shipping app does.
var diagDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(@"{
 ""os"":{""edition"":""Windows 11 Home"",""build"":""22631"",""hostname"":""SMOKE-PC""},
 ""memory"":{""used_gb"":13,""total_gb"":16,""percent_used"":82},
 ""disk"":[{""device"":""C:"",""percent_used"":91,""free_gb"":22,""total_gb"":256}],
 ""temp_files"":[{""size_mb"":740}], ""browser_caches"":[{""browser"":""Chrome"",""size_mb"":610}],
 ""recycle_bin"":{""size_mb"":1300},
 ""update_leftovers"":{""windows_old_present"":true,""windows_old_gb"":9,""windows_update_cache_mb"":0,""delivery_optimization_mb"":0},
 ""network"":{""connectivity"":{""internet_reachable"":true}},""dns_cache"":{""entries"":0},""bsod_history"":[],""device_errors"":[],""startup_programs"":[]
}")!;
foreach (var key in SystemAuditor.ExpectedKeys)
    if (!diagDict.ContainsKey(key)) diagDict[key] = new Dictionary<string, object?>();
var audit = SystemAuditor.Assess(diagDict);
var diag = audit.Diagnostics;   // validated evidence used for admissibility + validation

var planner = new GemmaLocalPlanner(inf);
var r = await planner.AuthorAsync(audit, env.AdmissibleSet(diag), new CancellationTokenSource(TimeSpan.FromSeconds(180)).Token);

int fail = 0;
void Check(bool ok, string label) { Console.WriteLine((ok ? "  PASS " : "  FAIL ") + label); if (!ok) fail++; }
string Trunc(string s) => s.Length <= 160 ? s : s.Substring(0, 160) + "…";
string provenancePath = Path.Combine(modelDir, "aegis-formation-manifest.json");
Check(File.Exists(provenancePath), "formation manifest travels with the selected model");
if (File.Exists(provenancePath))
{
    using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(provenancePath));
    Check(provenance.RootElement.GetProperty("model_name").GetString() == ProductModel.Name,
        "formation manifest identifies the Product responder");
    Check(provenance.RootElement.GetProperty("canon_sha256").GetString()
          == "CF9F9C8E08E0AC2245C5D3DC0FDFFE1DC155F470E1A7BDD4D5B3543F76DDDC2F",
        "formation manifest binds the locked Canon fingerprint");
}
string nature = NatureLayerContext.ForPlan();
Check(nature.Contains("Storage & disk health", StringComparison.Ordinal)
      && nature.Contains("System integrity", StringComparison.Ordinal),
    "packaged Product Nature Layer is available as bounded evidence-domain context");
Check(audit.IsValid && audit.SectionsPresent == SystemAuditor.ExpectedCount, "System Audit completed + validated (25/25 sections)");
Check(r != null, "on-device author returned a plan");
Check(r != null && r.Provider == "local-model", "provenance.provider == local-model");
if (r != null)
{
    var (plan, _) = env.Validate(r.Plan, diag);
    Check(plan["actions"]!.AsArray().Count > 0, "plan has >=1 admissible action after validation");
    string summary = r.Plan.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
    Check(summary.Length > 0, "plan carries a non-empty summary");
    Check(summary.Contains("91", StringComparison.Ordinal) || summary.Contains("740", StringComparison.Ordinal)
          || summary.Contains("1300", StringComparison.Ordinal) || summary.Contains("22", StringComparison.Ordinal),
        "plan summary grounds itself in a supplied diagnostic observation");
}

// ── Phase 2: conversational Custodian — prove a 2-turn on-device exchange ──
Console.WriteLine("\nCUSTODIAN CHAT (Phase 2 — multi-turn inference):");
// Minimal role framing (Phase 2 placeholder — the Canon seed + Reflective Boundary
// Conditions land here in a later phase). Without any system prompt the bare 1B model
// completes text instead of answering; the role turns it into a responder.
var chat = new CustodianChat(inf,
    "You are the ProntoPC Custodian, a warm, plain-English PC repair technician talking to a " +
    "worried customer. Answer their question directly in one or two short sentences. Never repeat " +
    "their question back. Never invent specifics you were not given.");
var chatCt = new CancellationTokenSource(TimeSpan.FromSeconds(240)).Token;

// An "echo" reply is the bug: the model parrots the question back instead of answering.
// A genuine reply is non-empty AND does not contain the question verbatim.
bool NotEcho(string? reply, string question) =>
    !string.IsNullOrWhiteSpace(reply) &&
    reply.IndexOf(question.Trim(), StringComparison.OrdinalIgnoreCase) < 0;

string q1 = "Hi — in one sentence, what will your diagnostic scan check on my PC?";
string? a1 = await chat.ReplyAsync(q1, chatCt);
Check(!string.IsNullOrWhiteSpace(a1), "chat turn 1: Custodian replied");
if (a1 != null) Console.WriteLine("    turn1> " + Trunc(a1));
Check(NotEcho(a1, q1), "chat turn 1: reply is an ANSWER, not an echo of the question");

string q2 = "Thanks. Will that scan change anything on my computer?";
string? a2 = await chat.ReplyAsync(q2, chatCt);
Check(!string.IsNullOrWhiteSpace(a2), "chat turn 2: Custodian replied (history carried forward)");
if (a2 != null) Console.WriteLine("    turn2> " + Trunc(a2));
Check(NotEcho(a2, q2), "chat turn 2: reply is an ANSWER, not an echo of the question");
Check(a2 != null && a2.IndexOf("read-only", StringComparison.OrdinalIgnoreCase) >= 0
    && a2.IndexOf("changes nothing", StringComparison.OrdinalIgnoreCase) >= 0,
    "chat turn 2: read-only scan is grounded as changing nothing");
Check(a2 != null && a2.IndexOf("reboot", StringComparison.OrdinalIgnoreCase) < 0
    && a2.IndexOf("restart", StringComparison.OrdinalIgnoreCase) < 0
    && a2.IndexOf("performance", StringComparison.OrdinalIgnoreCase) < 0,
    "chat turn 2: does not invent scan effects");
Check(chat.History.Count == 4, "history holds 4 turns (2 user, 2 model)");

Console.WriteLine(fail == 0 ? "\nMODEL SMOKE PASS" : $"\n{fail} FAILURE(S)");
Environment.Exit(fail == 0 ? 0 : 1);
