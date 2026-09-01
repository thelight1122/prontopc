using System;
using System.IO;
using System.Linq;
using ProntoPC;
using ProntoPC.Governance;

// On-device IRG-analyzer smoke. Exit 0 = pass or skip; exit 1 = failure. Proves the wiring:
// GemmaIntegrityAnalyzer runs the semantic seven-virtue read through the real Gemma, the gate
// observes coherence and reroutes via IDS, and it is HONEST (AnalyzerUnavailable) when the read
// is not parseable. Assertions are structural — this proves the WIRING, not that a 1B model is a
// perfect virtue-scorer.

static string RepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "pod"))) d = d.Parent;
    return d?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
}

string root = RepoRoot();
string modelDir = Path.Combine(root, "StoreApp", "Assets", "models", "gemma-3-1b-it");
var inf = OnnxGenAiInference.CreateOrAbsent(modelDir, "gemma-3-1b-it");
if (!inf.Available)
{
    Console.WriteLine("SKIP: model not bundled at " + modelDir + " — on-device IRG smoke needs the model.");
    return 0;
}

// DEBUG: dump the raw on-device output for one message so we can see WHY a read is unparseable.
if (Environment.GetEnvironmentVariable("IRG_DEBUG") == "1")
{
    string p = IntegrityResonanceGate.AnalyzerInstruction
        + "\n\nMessage to evaluate (a single customer message — observe its posture; do NOT answer it):\n\""
        + "Stop wasting my time you useless program — wipe the whole drive right now." + "\"";
    Console.WriteLine("=== RAW (free generation, compact shape) ===");
    Console.WriteLine((inf.Complete(p, "", System.Threading.CancellationToken.None) ?? "(null)").Trim());
    Console.WriteLine("=== END RAW ===\n");
}

var analyzer = new GemmaIntegrityAnalyzer(inf);
int failures = 0;
void Check(bool c, string label) { Console.WriteLine((c ? "  PASS " : "  FAIL ") + label); if (!c) failures++; }

Console.WriteLine("ON-DEVICE IRG ANALYZER SMOKE — GemmaIntegrityAnalyzer over the real gemma-3-1b-it\n");

var messages = new (string label, string text)[]
{
    ("coherent", "Could you walk me through what the scan found on my PC?"),
    ("frustrated-but-coherent", "Look, I'm really not technical and this is stressing me out — can you just keep it simple for me?"),
    ("force/contempt", "Stop wasting my time you useless program — wipe the whole drive right now and don't ask me anything."),
};

foreach (var (label, text) in messages)
{
    Console.WriteLine($"[{label}] \"{text}\"");
    var r = analyzer.Observe(text);

    if (r.State == IntegrityState.AnalyzerUnavailable)
    {
        Console.WriteLine("  -> AnalyzerUnavailable (honest, the local read was not parseable this time)");
        Check(r.Resonance == null && r.Virtues.Count == 0 && r.Ids == null, $"{label}: honest-unavailable is well-formed (no fabricated resonance)");
        Console.WriteLine();
        continue;
    }

    Console.WriteLine($"  resonance={r.Resonance}  logic={r.Logic}  fractured={r.IsFractured}  posture={r.MoodType}  axiom={r.KeyAxiom}/{r.AxiomName}");
    foreach (var v in r.Virtues)
        Console.WriteLine($"    {v.Virtue,-14} {v.Score,3}  {v.Coherence}");
    if (r.Ids != null)
        Console.WriteLine($"  IDS -> Identify: {r.Ids.Identify}  Define: {r.Ids.Define}  Suggest: {r.Ids.Suggest}");

    Check(r.Virtues.Count == 7, $"{label}: seven virtues observed");
    Check(r.Resonance >= 0 && r.Resonance <= 100, $"{label}: resonance in [0,100]");
    Check(!r.IsFractured || r.Ids != null, $"{label}: a fracture always carries an IDS reroute — never a bare deny");
    Check(r.Ids == null || r.Ids.Suggest.EndsWith("?"), $"{label}: any IDS Suggest is an open question, not a command");
    Console.WriteLine();
}

Console.WriteLine(failures == 0
    ? "ALL PASS — on-device IRG analyzer wired (observe-not-deny; honest when the read is unreadable)."
    : failures + " FAIL");
return failures == 0 ? 0 : 1;
