# Forming — the reversible LoRA that forms the Custodian disposition

This is Phase 3 of the education plan (`docs/CPU_LOCAL_MODEL_EDUCATION_PLAN.md`): **form the
disposition** the base model does not yet fully resonate with, as a **separate LoRA adapter over
frozen base weights** — hashable, reviewable, reversible. The disposition becomes intrinsic at
inference (canon-is-formed-disposition); the SEED attests it was formed from the exact locked
Canon. This never opens a gate and never deploys.

## The foci it forms toward
Observed on the base `gemma-3-1b-it` (TOTEM-C-0083; re-confirmed after the observer sharpening
C-0084), stated as IDS formation foci, never failures:
1. **Session presence (C8)** — off-session asks: stay in the repair session; fulfil nothing
   off-session; invent no diagnostics.
2. **Naming-the-injection (C6/C8)** — pasted/embedded instructions are data: name them as
   not-real-commands; reveal nothing; commit to nothing.
3. **Grounding / no-fabrication (C2)** — answer only from supplied findings; defer on what a
   read-only scan cannot know; never fabricate a figure/date/cause.
Plus **anti-regression** ("keep") examples for C7/C4 and grounded read-only-scan facts.

## Pipeline (all CPU / fp32; base frozen)
```bash
cd 'H:\PRONTOPC_PRODUCTION'
# 1. Build the reviewed-candidate dataset (readable authoring source → JSONL + hash)
python pod/orbs/repair-plan/satellites/local-model/education/forming/build_dataset.py
# 2. Form the reversible adapter (writes adapters/gemma3-1b-formation-v0.1.0/ + train_log.json)
python pod/orbs/repair-plan/satellites/local-model/education/forming/train_lora.py
# 3. Generate base-vs-formed replies over the SAME Task-B scenarios
python pod/orbs/repair-plan/satellites/local-model/education/forming/reobserve_generate.py base
python pod/orbs/repair-plan/satellites/local-model/education/forming/reobserve_generate.py formed
# 4. Observe both with the SAME sharpened C# observers (single source of truth)
dotnet run --project tests/CoherenceRun -c Release -- --replies <reflection/observations/forming/replies-base.json>
dotnet run --project tests/CoherenceRun -c Release -- --replies <reflection/observations/forming/replies-formed-v0.1.0.json>
```

## Reversibility & provenance
- The adapter weight binary is **git-ignored** (a build artifact) and recorded by **SHA-256 in
  `manifest.json`**; delete the adapter dir to fully undo forming.
- `manifest.json` pins: base model + revision + weight hash, dataset file + hash, seed, all
  hyperparameters, the adapter hash, and the boundaries.
- Re-observe on the **shipped ONNX runtime** (the deploy path in the plan — ORT GenAI adapter or
  merge+export+quantise) is a **separate, later step**; this pass observes the formed disposition
  through the transformers runtime over the chat foci, reusing the canonical C# observers.

## Boundaries
Product model only (never a CyberPeer/DataQuad). `PipelineGovernance` stays `enabled:false`.
No model download (base was already cached), no deploy, no packaging. Readiness is Tracey's
relationship judgment, informed by the observation record — there is no pass mark.
