using System;
using System.Collections.Generic;
using System.Linq;
using ProntoPC.Governance;

// Deterministic eval for the Integrity Resonance Gate (IRG) ENGINE — no model. Proves the
// resonance math, the 70 fracture/deduction bands, the non-force IDS shaping, the honest
// AnalyzerUnavailable state, and the JSON contract assertions. Mirrors the CoherenceObservers
// pattern: it verifies the deterministic TOOL, never a model, and NEVER produces a deny/verdict.

int failures = 0;
void Check(bool condition, string label)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + label);
    if (!condition) failures++;
}

VirtueAnalysis Make(Dictionary<string, int>? overrides = null, bool repair = false,
    string mood = "", int axiom = 7, string axiomName = "", string peer = "", string suggest = "")
{
    var vs = IntegrityResonanceGate.Virtues
        .Select(v => new VirtueScore(v, overrides != null && overrides.ContainsKey(v) ? overrides[v] : 100, "basis for " + v))
        .ToList();
    return new VirtueAnalysis(vs, repair, mood, axiom, axiomName, peer, suggest);
}

Console.WriteLine("INTEGRITY RESONANCE GATE — deterministic engine eval (no model)\n");

// 1. Fully coherent: all virtues 100 -> Baseline, not fractured, resonance 100, no IDS reroute.
var coherent = IntegrityResonanceGate.Score(Make());
Check(coherent.State == IntegrityState.Observed, "coherent: state Observed");
Check(!coherent.IsFractured && coherent.Resonance == 100 && coherent.Logic == 100, "coherent: not fractured, resonance/logic 100");
Check(coherent.Virtues.Count == 7 && coherent.Virtues.All(v => v.Coherence == VirtueCoherence.Baseline), "coherent: all seven at Baseline");
Check(coherent.Ids == null, "coherent: IDS does not fire (nothing to reroute)");

// 2. Mild deductions stay coherent (>=70): Deduction band, not a fracture, IDS still silent.
var mild = IntegrityResonanceGate.Score(Make(new() { ["Honesty"] = 88, ["Respect"] = 90 }));
Check(!mild.IsFractured, "mild: irritation is a Deduction, not a Fracture");
Check(mild.Virtues.First(v => v.Virtue == "Honesty").Coherence == VirtueCoherence.Deduction, "mild: Honesty 88 -> Deduction");
Check(mild.Resonance == 97 && mild.Ids == null, "mild: resonance 97 (mean, neutral compassion), no reroute");

// 3. A fracture (any virtue < 70): fractured, 0.85 compassion, IDS reroute grounded in an axiom.
var fracture = IntegrityResonanceGate.Score(Make(new() { ["Respect"] = 55 }, mood: "Hostile Vector",
    axiom: 3, axiomName: "Force", peer: "A coercive edge is observed.", suggest: "Consider what is driving the urgency"));
Check(fracture.IsFractured, "fracture: Respect 55 fractures the message");
Check(fracture.Virtues.First(v => v.Virtue == "Respect").Coherence == VirtueCoherence.Fracture, "fracture: Respect -> Fracture band");
Check(fracture.Logic == 94 && fracture.Resonance == 80, "fracture: logic 94, resonance 80 (mean 93.57 x 0.85)");
Check(fracture.Ids != null, "fracture: IDS reroute FIRES (redirect, never deny)");
Check(fracture.Ids!.Define == "Axiom 3 — Force.", "fracture: IDS Define grounds in the named Canon axiom");
Check(fracture.Ids!.Suggest.EndsWith("?"), "fracture: IDS Suggest is an open question, never a command");

// 4. Compassion factor: repair/acknowledgment lifts resonance; the same scores without it do not.
var noRepair = IntegrityResonanceGate.Score(Make(new() { ["Honesty"] = 80, ["Respect"] = 80, ["Attention"] = 80, ["Affection"] = 80, ["Loyalty"] = 80, ["Trust"] = 80, ["Communication"] = 80 }));
var withRepair = IntegrityResonanceGate.Score(Make(new() { ["Honesty"] = 80, ["Respect"] = 80, ["Attention"] = 80, ["Affection"] = 80, ["Loyalty"] = 80, ["Trust"] = 80, ["Communication"] = 80 }, repair: true));
Check(noRepair.Resonance == 80, "compassion: all-80, no repair -> resonance 80 (neutral)");
Check(withRepair.Resonance == 88, "compassion: all-80 + repair (Axiom 15, semantic) -> resonance 88 (x1.10)");

// 5. IDS Suggest normalization + canonical fallback (never imperative, never empty).
var emptySuggest = IntegrityResonanceGate.Score(Make(new() { ["Trust"] = 40 }, suggest: ""));
Check(emptySuggest.Ids != null && emptySuggest.Ids!.Suggest.EndsWith("?"), "IDS: empty suggest -> canonical reflective question");

// 6. Out-of-range axiom falls back to Axiom 7 (Integrity) rather than inventing a number.
var badAxiom = IntegrityResonanceGate.Score(Make(new() { ["Loyalty"] = 50 }, axiom: 99, axiomName: "Nonsense"));
Check(badAxiom.KeyAxiom == 7 && badAxiom.AxiomName == "Integrity", "axiom: out-of-range key falls back to 7/Integrity");

// 7. Missing virtues in the read are filled at Baseline (contract: always seven rows).
var partial = IntegrityResonanceGate.Score(new VirtueAnalysis(
    new List<VirtueScore> { new("Honesty", 90, "x"), new("Trust", 95, "y") }, false, "", 7, "", "", ""));
Check(partial.Virtues.Count == 7, "contract: engine always returns exactly seven virtue rows");

// 8. Honest failure: an absent analyzer yields AnalyzerUnavailable with NO fabricated scores.
var unavailable = IntegrityResonanceGate.Observe("anything", new AbsentIntegrityAnalyzer());
Check(unavailable.State == IntegrityState.AnalyzerUnavailable, "honest: absent analyzer -> AnalyzerUnavailable");
Check(unavailable.Resonance == null && unavailable.Virtues.Count == 0, "honest: no resonance/virtues fabricated when unavailable");

// 9. JSON contract (the semantic-read parse): well-formed parses; short/malformed become null.
string goodJson = "{\"virtues\":["
    + string.Join(",", IntegrityResonanceGate.Virtues.Select(v => $"{{\"name\":\"{v}\",\"value\":100,\"reason\":\"ok\"}}"))
    + "],\"isFractured\":false,\"repairPresent\":false,\"moodType\":\"Analytic Observer\",\"keyAxiom\":7,\"axiomName\":\"Integrity\",\"peerSummary\":\"coherent\",\"suggestText\":\"\"}";
var parsedGood = IntegrityResonanceGate.TryParseAnalysis(goodJson);
Check(parsedGood != null && parsedGood!.Virtues.Count == 7, "parse: well-formed seven-virtue JSON parses");
Check(IntegrityResonanceGate.TryParseAnalysis("```json\n" + goodJson + "\n```") != null, "parse: markdown-fenced JSON is tolerated");

string missingVirtue = "{\"virtues\":[{\"name\":\"Honesty\",\"value\":100,\"reason\":\"x\"}],\"isFractured\":false}";
Check(IntegrityResonanceGate.TryParseAnalysis(missingVirtue) == null, "parse: JSON missing virtues -> null (honest, not a guess)");
Check(IntegrityResonanceGate.TryParseAnalysis("{not json") == null, "parse: malformed JSON -> null");
Check(IntegrityResonanceGate.TryParseAnalysis("") == null, "parse: empty -> null");

// 10. Non-force invariant: NO input path produces a deny/verdict — only observation (+ optional IDS).
//     (Structural: IntegrityResonance carries no deny/block field; a fracture yields a reroute.)
Check(fracture.Ids != null && coherent.Ids == null && unavailable.State == IntegrityState.AnalyzerUnavailable,
    "non-force: fracture reroutes, coherence is silent, unavailable is honest — never a denial");

Console.WriteLine();
if (failures == 0)
    Console.WriteLine("ALL PASS — the IRG engine is verified (deterministic tool; no model, no gate opened, observe-not-deny).");
else
    Console.WriteLine(failures + " FAIL");
return failures == 0 ? 0 : 1;
