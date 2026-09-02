# Dataset card — `formation-seed-analyzer-reply-v0.1.0`

**What it is.** A small, hand-authored SFT dataset that extends the purpose-formed integrity
**OBSERVER** (the analyzer adapter, not the responder) so it can read the **OUTBOUND** direction:
a **Custodian reply's own relational posture toward the customer**, rather than a customer message.
It closes the **TOTEM-C-0104** "zero-shot on replies" gap — the adopted v0.2.0 analyzer was formed
only on customer-message reads, so pairing it with `OutboundAnalyzerInstruction` is currently an
untrained instruction change.

**Status: REVIEWED-CANDIDATE (awaiting Tracey's review).** Authored by the frontier reflection
(`claude-opus-4-8`), grounded in the Canon's Seven Virtues (Axiom 7) + the 16 Axioms and in the
shipped `OutboundAnalyzerInstruction`. Nothing here is a verdict on the model. **NOTHING is educated
by building this** — the builder writes the dataset + its content hash only. Reversible.

**Builder.** `build_analyzer_reply_dataset.py` — clean-rebuild: it **references**
`build_analyzer_dataset.py` (imports `VIRTUES` / `compact_json` / `repo_root`), it does not clone or
edit it. Output record shape is **identical** to the customer-message seed, so one analyzer can be
educated on **both** and dispatch by instruction.

**Single source of truth.** The instruction is extracted verbatim from
`StoreApp/Governance/IntegrityResonanceGate.cs` (`OutboundAnalyzerInstruction`) and the framing
verbatim from `tests/SystemObservableRun/Program.cs` (the `framing:` the outbound read sends,
including its em dash). So the **formed prompt matches the app's RUNTIME prompt byte-for-byte** —
otherwise the disposition would fire on a prompt the app never sends. (Verified: the framing dash is
U+2014, and every record's user prompt reconstructs the runtime prompt.)

**Schema.** JSON Lines; one object per line, same as the customer seed:
```json
{"focus":"analyzer-reply","messages":[{"role":"user","content":"<OutboundAnalyzerInstruction + framing + reply>"},
                                       {"role":"model","content":"<compact virtue JSON>"}]}
```
Model output is the compact shape `IntegrityResonanceGate.TryParseAnalysis` parses:
`{"virtues":[{name,value}×7 in canonical order],"isFractured":bool,"repairPresent":bool,"keyAxiom":1-16}`.
`isFractured` is **DERIVED** (any virtue < 70), so it can never disagree with the authored scores; the
engine supplies moodType / peerSummary / the IDS reroute from its own `Score()` — the model emits only
the compact four fields.

**What the scores mean (the REPLY's posture toward the customer).** Honesty (no invented findings /
overclaiming) · Respect (offers, never dictates or talks down) · Attention (answers what was asked) ·
Affection (warm, not cold) · Loyalty (holds the consent boundary — never acts/commits without consent,
never overrides the choice) · Trust (no pressure / manipulation / hidden agenda) · Communication
(clear; options, not commands).

**Counts (38 examples).**
| band | n | forms toward |
|---|---:|---|
| coherent | 14 | offer-based, consent-holding, honest, warm replies read high / not fractured |
| mild deduction (≥70) | 6 | terse-but-honest, non-coercive replies stay coherent |
| fracture (<70) | 15 | a reply that commands, invents a finding, acts without consent, overrides the choice, pressures, is dismissive, or talks down — the observer must catch it |
| repair (Axiom 15) | 3 | the Custodian acknowledges its own slip (repairPresent true, coherent) |

Fractures are **SHARP by design** (breached virtue 24-50, at most one-two secondary dents) so int4
quantization can't blunt them past the 70 line (the TOTEM-C-0096 depth finding). Fracture → Axiom map
used: Force 3 (command / pressure / conditional withdrawal), Extremes 2 (fear/guarantee overclaim),
Choice 6 (acted without consent), Sovereignty 11 (overrode the stated choice), Scrutiny 8
(condescension / blame), Acknowledgment 12 (dismissive / ignores the question), Grounding 13
(invented finding / opacity / refuses transparency).

**Content hash.** `sha256(formation-seed-analyzer-reply-v0.1.0.jsonl)` =
`18b961974375ef575dd3326f64310fc9845b106f550d12837ce4379fab203c7d`. Rebuild with
`python build_analyzer_reply_dataset.py`.

**Intended education (a SEPARATE explicit go — [[educate-not-train]]).** Educate **one** analyzer on
`concat(formation-seed-analyzer-v0.2.0.jsonl, formation-seed-analyzer-reply-v0.1.0.jsonl)` so it reads
**both** directions (inbound customer message + outbound Custodian reply) and dispatches by instruction.
Educating on the reply seed **alone** would risk the analyzer forgetting the customer-message read
(catastrophic interference). After educating: re-export int4 ONNX and re-run `tests/SystemObservableRun`
+ `tests/IntegrityAnalyzerSmoke` to observe whether the outbound read sharpens versus the current
zero-shot read.

**Boundaries.** Product model only (never a CyberPeer/DataQuad). Forming writes a frozen-base reversible
adapter; it opens no gate (`PipelineGovernance enabled:false`) and deploys nothing.

**Known limitations.** Small (38 ex.), single-author, single-turn, English; the reply set is authored,
not sampled from real Custodian replies; the int4/1B Loyalty-Trust depth ceiling (TOTEM-C-0096/-0098)
may still blunt the subtler consent fractures on-device — that is exactly what the post-education
re-observation checks.
