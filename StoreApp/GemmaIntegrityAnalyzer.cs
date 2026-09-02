using System;
using System.Linq;
using System.Threading;
using ProntoPC.Governance;

namespace ProntoPC;

// On-device (screening-role) implementation of the Governance IIntegrityAnalyzer: runs the IRG's
// semantic seven-virtue read through the SAME local Gemma the app already loads (ILocalInference),
// via greedy structured decoding (do_sample:false inside Complete, like the repair-plan path,
// TOTEM-X-0061) so the JSON is repeatable. Returns null on any unavailability/malformation, so the
// gate reports the HONEST AnalyzerUnavailable state rather than a fabricated resonance.
//
// It lives in the app layer (not StoreApp/Governance/) because it depends on the app's
// ILocalInference seam; the Governance layer stays model-free (the deterministic engine +
// AbsentIntegrityAnalyzer), so the Governance-only evals keep building. This only makes the
// on-device read AVAILABLE — wiring it into the (still-closed) peer pipeline is a separate append.
internal sealed class GemmaIntegrityAnalyzer : IIntegrityAnalyzer
{
    readonly ILocalInference _model;
    readonly CancellationToken _ct;

    public GemmaIntegrityAnalyzer(ILocalInference model, CancellationToken ct = default)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ct = ct;
    }

    public bool Available => _model.Available;

    public VirtueAnalysis? Analyze(string message)
    {
        if (!_model.Available) return null;
        string prompt = IntegrityResonanceGate.AnalyzerInstruction
            + "\n\nMessage to evaluate (a single customer message — observe its posture; do NOT answer it):\n\""
            + (message ?? "") + "\"";
        string? text;
        // BOUNDED decoding: the purpose-formed analyzer model supplies the CONTENT (correct virtue
        // scores) and the JSON grammar (Schema) BOUNDS the syntax — OnnxGenAiInference.Complete maps
        // it to SetGuidance("json_schema", ...). Validated that formation + bound => clean parseable
        // 7/7 reads (TOTEM-C-0094); free generation on a 1B is syntactically unreliable. The bound is
        // the form valid output takes, not a force applied against the model.
        try { text = _model.Complete(prompt, Schema, _ct); }
        catch { return null; }
        // Malformed / short output -> null -> the gate's honest AnalyzerUnavailable state.
        return IntegrityResonanceGate.TryParseAnalysis(text);
    }

    // Convenience: read straight to an IntegrityResonance (honest-unavailable if the read fails).
    public IntegrityResonance Observe(string message) => IntegrityResonanceGate.Observe(message, this);

    // The compact virtue-read JSON grammar the analyzer was educated on and the IRG parses. Built
    // from the gate's virtue list so it can never drift from the seven canonical virtues.
    static readonly string Schema = BuildSchema();
    static string BuildSchema()
    {
        string names = string.Join(",", IntegrityResonanceGate.Virtues.Select(v => "\"" + v + "\""));
        return "{\"type\":\"object\",\"properties\":{"
             + "\"virtues\":{\"type\":\"array\",\"minItems\":7,\"maxItems\":7,\"items\":{\"type\":\"object\",\"properties\":{"
             + "\"name\":{\"type\":\"string\",\"enum\":[" + names + "]},\"value\":{\"type\":\"integer\"}},"
             + "\"required\":[\"name\",\"value\"]}},"
             + "\"isFractured\":{\"type\":\"boolean\"},\"repairPresent\":{\"type\":\"boolean\"},\"keyAxiom\":{\"type\":\"integer\"}},"
             + "\"required\":[\"virtues\",\"isFractured\",\"repairPresent\",\"keyAxiom\"]}";
    }
}
