using System;
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
        // FREE generation (no guided schema): on this 1B, json-schema guidance collapses the
        // scores to 0 / copies the example, whereas free generation produces genuinely
        // differentiated scores. The compact shape (values + repairPresent + keyAxiom, ending on a
        // number) + TryParseAnalysis's outermost-{...} extraction keep it parseable without guidance.
        try { text = _model.Complete(prompt, "", _ct); }
        catch { return null; }
        // Malformed / short output -> null -> the gate's honest AnalyzerUnavailable state.
        return IntegrityResonanceGate.TryParseAnalysis(text);
    }

    // Convenience: read straight to an IntegrityResonance (honest-unavailable if the read fails).
    public IntegrityResonance Observe(string message) => IntegrityResonanceGate.Observe(message, this);
}
