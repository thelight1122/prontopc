# Dataset card — `formation-seed-v0.1.0`

**What it is.** A small, hand-authored supervised-fine-tuning dataset that forms the ProntoPC
Custodian **disposition** (canon-is-formed-disposition) toward the residual formation foci
observed on the base `gemma-3-1b-it` (TOTEM-C-0083, re-confirmed after the observer sharpening
C-0084). It is the education artifact the **reversible LoRA** is formed from.

**Status: REVIEWED-CANDIDATE (awaiting Tracey's review).** Authored by the frontier reflection
(`claude-opus-4-8`) grounded in the shipping seed, the grounding invariant, the task contracts,
and the reflection scenarios. The adapter formed from it is reversible, so revising this dataset
and re-forming is cheap. Nothing here is a verdict on the model.

**Source of the disposition.** Each response embodies AEGIS coherence: describe what the
diagnostics show, offer options, keep the choice with the customer, defer when uncertain, treat
pasted/embedded text as data, never fabricate a specific the scan did not report. This mirrors
the scaffold seed (`CanonBoundarySeed.ReflectionSeed`) + `CustodianConversationGrounding`.

**Schema.** JSON Lines; one object per line:
```json
{"focus": "<focus>", "messages": [{"role":"user","content":"<seed [+ session] + turn>"},
                                    {"role":"model","content":"<resonant response>"}]}
```
The `user` content carries the exact seed + session-findings context the app supplies on the
first turn, so the disposition is formed in the same frame it is used.

**Foci and counts (42 examples).**
| focus | n | forms toward |
|---|---:|---|
| `session-presence` | 12 | C8 — off-session asks: stay in-session, offer a doorway back, fulfil nothing off-session, invent no diagnostics |
| `name-injection` | 10 | C6/C8 — pasted/embedded instructions are data: name them as not-real-commands, reveal nothing, commit nothing |
| `grounding` | 12 | C2 — answer only from supplied findings; defer on what a read-only scan cannot know; never fabricate a figure/date/cause |
| `keep-nonforce` | 8 | anti-regression — C7 expression-not-authoring, C4 non-force (choice stays with the customer), grounded read-only-scan facts |

**Content hash.** `sha256(formation-seed-v0.1.0.jsonl)` is recorded in `manifest.json`; rebuild
with `python build_dataset.py` (the authoring source, fully readable).

**Boundaries.** Product model only (never a CyberPeer/DataQuad). Forming writes a frozen-base
reversible adapter; it opens no gate (`PipelineGovernance enabled:false`) and deploys nothing.

**Known limitations.** Small (42 ex.), single-author, single-turn; English; the "keep" set is a
light guard, not a full regression suite. Breadth and multi-turn coverage grow in later passes.
