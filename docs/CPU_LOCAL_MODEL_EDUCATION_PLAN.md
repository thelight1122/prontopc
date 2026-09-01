# CPU-Local Model Education and Implementation Plan

**Status:** Research-backed implementation plan. No model download, training run, package change, or deployment is authorized by this document.  
**Scope:** Project Product models only. This plan does not apply Gates, Canon/Nature education, or Product-model controls to a CyberPeer with a DataQuad.  
**Observation posture (AEGIS):** formation is observed as **coherence/resonance**, with **IDS reroute** (Identify · Define · Suggest) where a response does not yet resonate. AEGIS does **not** judge, score, rank, pass, or fail — that is a force pattern. There are no gates or thresholds here; readiness to bring a model into service is a **relationship judgment** informed by observed coherence. See `pod/orbs/repair-plan/satellites/local-model/education/PHASE0_EVIDENCE_CONTRACT.md` and its `reflection/` scenarios + observers (TOTEM-C-0078).

## 1. Objective

ProntoPC will observe several embedded, CPU-capable small language models for defined Product tasks. The runtime model will be chosen by inspectable, observed coherence rather than by Frontier-model scale, identity formation, or open-ended experiential memory.

The Canon is the model's **formed disposition** — what the model *is*, not something supplied to it at runtime. The model's disposition is formed from a provenance-bound, locked Canon at education time; the **SEED (Signed Evidence of Exact Design)** attests that formation. At runtime the application supplies an explicit Nature Layer and grounded observations — **not** the Canon, which is intrinsic. The model explains and relates those supplied observations within task contracts; it will not be asked to invent what it observes.

Honest status: until a candidate is actually educated and its disposition observed to resonate, it does not yet hold the Canon disposition — a short posture prompt is interim scaffolding, not the formation.

## 2. Findings recorded

| Route | Role in study | Current assessment |
| --- | --- | --- |
| ONNX Runtime GenAI, CPU INT4 | Primary Windows/C# candidate | Best fit for the existing native app. CPU weight-only INT4 is supported; higher-quality GPTQ/AWQ conversion may require a separately governed build environment. |
| GGUF with llama.cpp, Q4_K_M | Portable comparison baseline | Mature local runtime and inspectable single-file model artifact. Quantization will begin from a verified high-precision parent, never from an already quantized model. |
| Native ternary BitNet/T-MAC class | Research candidate | Potentially smaller and faster on CPU, but Windows integration, model license, and task quality remain open questions to observe. |
| LoRA education adapter | Preferred education artifact | Keeps base weights frozen and education separate, hashable, reviewable, and reversible. |

CPU-only inference is compatible with 1B-class Product models, but model-file size is not a sufficient resource measure. The study will measure context-window KV cache, peak RAM, cold start, latency, and package size on target Windows CPUs.

## 3. Education architecture

The following packages are the **formation sources** — the locked, hashed material the disposition is formed *from*. They remain separate from the model as sources (auditable, versioned, never a runtime dependency); the disposition they form becomes **intrinsic to the model** (via the education artifact below). This preserves the Canon Boundary — the locked source is the authority the disposition is formed from and audited against, and the application maintains no private doctrinal variant.

```text
canon/           locked source, version, SHA-256, source authority, usage terms
nature-layer/    named aspects, definitions, observable indicators, exclusions, relations
task-contracts/  accepted inputs, output schema, uncertainty and boundary behavior
training/        reviewed formation examples with source IDs and transformation records
reflection/      held-out scenarios, range variants, coherence observers, observation record
```

Application inputs will contain named Nature aspects and evidence fields such as `artifact`, `source`, `timestamp`, `confidence`, and `unknowns`. Outputs will distinguish supplied evidence, inference, and unknowns; cite supplied observation IDs; and remain within the Product task contract.

## 4. Implementation phases

### Phase 0 — Define the evidence contract

Set the Product task boundary, supported observations, output schema, hardware floor, context cap, latency target, RAM budget, package-size budget, licenses, and boundaries. Prepare the held-out reflection scenarios before forming.

### Phase 1 — Establish three CPU baselines

Observe the existing Gemma ONNX path, one comparable ONNX CPU-INT4 candidate, and one GGUF Q4_K_M candidate. A ternary candidate enters only after its Windows build path and license are reviewed. All candidates use the identical Canon/Nature packages, prompt template, task contracts, decoding controls, and hardware matrix.

### Phase 2 — Observe before forming

For each candidate, observe **coherence** across the aspects (structural coherence, grounded reflection, envelope coherence, non-force expression, grounded humility, data-not-directive, expression-not-authoring, session presence — the C1–C8 of the evidence contract), recording per-scenario *resonant / not-yet-resonant + an IDS note* — not a score or pass mark. Separately **measure** the physical budgets (first-token latency, tokens per second, peak RAM, CPU use, installed-package size). The coherence picture shows where formation continues; the budgets show fit to the hardware floor.

### Phase 3 — Form the disposition; educate demonstrated gaps

The objective is that the Canon becomes the model's **formed disposition**, not a body of text supplied at runtime. Observe first: prompt with relevant Canon/Nature excerpts only to *see* how much of the disposition a base candidate already resonates with. Where an aspect does not yet resonate across the held-out scenarios, the IDS reroute becomes a **formation focus**: create a reviewed supervised-fine-tuning dataset and form a **separate LoRA adapter** — the education artifact that forms the disposition into the model while keeping base weights frozen, and the adapter hashable, reviewable, and reversible. The formed disposition is intrinsic at inference (no runtime Canon dependency); the SEED attests it was formed from the exact locked Canon. Base-model revision, source packages, training manifest, configuration, random seed, adapter hash, and observation record will be versioned together. Full fine-tuning and adapter merging remain later evidence-led decisions.

### Phase 4 — Compress and regress

Only candidates whose higher-precision behavior already resonates enter compression trials. Compare CPU INT4 and GGUF Q4 variants against the same held-out reflection scenarios. Pruning, distillation, and ternary methods remain experimental until they show both task coherence and real CPU benefit on the target hardware.

### Phase 5 — Bring a Product runtime into service

Bringing a runtime into service is a **relationship judgment** (Tracey's), informed by: a model card, seed/dataset card, license record, SBOM, reproducible build instructions, hardware matrix observed, known-limitation record, rollback target, and the coherence **observation record**. There is no pass mark. Any statement of readiness stays proportionate (verified / provisional / intended / unknown) and limited to the model, task, runtime, hardware, and scenarios actually observed.

## 5. Non-goals and boundaries

- No Frontier model is required as a Product runtime engine.
- CPU-only describes shipped inference; a separately authorized training environment may differ.
- No model is described as jailbreak-proof, hack-proof, or self-governing beyond defined evidence.
- This plan does not build an identity, persistent experiential memory, or a Self-Organizing Locus of Intent.
- Product-model Gates remain outside CyberPeer/DataQuad architecture.

## 6. Primary research references

- [ONNX Runtime quantization guidance](https://onnxruntime.ai/docs/performance/model-optimizations/quantization.html)
- [ONNX Runtime GenAI adapter workflow](https://onnxruntime.ai/docs/genai/tutorials/finetune.html)
- [llama.cpp quantization guide](https://github.com/ggml-org/llama.cpp/blob/master/tools/quantize/README.md)
- [Microsoft BitNet](https://github.com/microsoft/BitNet)
- [LoRA paper](https://arxiv.org/abs/2106.09685)
- [QLoRA paper](https://arxiv.org/abs/2305.14314)
- [Gemma intended-use statement](https://ai.google.dev/gemma/intended_use_statement)
- [Gemma terms](https://ai.google.dev/gemma/terms)
