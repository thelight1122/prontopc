# CPU-Local Model Education and Implementation Plan

**Status:** Research-backed implementation plan. No model download, training run, package change, or deployment is authorized by this document.  
**Scope:** Project Product models only. This plan does not apply Gates, Canon/Nature education, or Product-model controls to a CyberPeer with a DataQuad.

## 1. Objective

ProntoPC will evaluate several embedded, CPU-capable small language models for defined Product tasks. The runtime model will be selected by inspectable evidence rather than by Frontier-model scale, identity formation, or open-ended experiential memory.

The Canon is the model's **formed disposition** — what the model *is*, not something supplied to it at runtime. The model's disposition is formed from a provenance-bound, locked Canon at education time; the **SEED (Signed Evidence of Exact Design)** attests that formation. At runtime the application supplies an explicit Nature Layer and grounded observations — **not** the Canon, which is intrinsic. The model explains and relates those supplied observations within task contracts; it will not be asked to invent what it observes.

Honest status: until a candidate is actually educated and its disposition evaluated, it does not yet hold the Canon disposition — a short posture prompt is interim scaffolding, not the formation.

## 2. Findings recorded

| Route | Role in study | Current assessment |
| --- | --- | --- |
| ONNX Runtime GenAI, CPU INT4 | Primary Windows/C# candidate | Best fit for the existing native app. CPU weight-only INT4 is supported; higher-quality GPTQ/AWQ conversion may require a separately governed build environment. |
| GGUF with llama.cpp, Q4_K_M | Portable comparison baseline | Mature local runtime and inspectable single-file model artifact. Quantization will begin from a verified high-precision parent, never from an already quantized model. |
| Native ternary BitNet/T-MAC class | Research candidate | Potentially smaller and faster on CPU, but Windows integration, model license, and task quality remain evidence gates. |
| LoRA education adapter | Preferred education artifact | Keeps base weights frozen and education separate, hashable, reviewable, and reversible. |

CPU-only inference is compatible with 1B-class Product models, but model-file size is not a sufficient resource measure. The study will measure context-window KV cache, peak RAM, cold start, latency, and package size on target Windows CPUs.

## 3. Education architecture

The following packages are the **formation sources** — the locked, hashed material the disposition is formed *from*. They remain separate from the model as sources (auditable, versioned, never a runtime dependency); the disposition they form becomes **intrinsic to the model** (via the education artifact below). This preserves the Canon Boundary — the locked source is the authority the disposition is formed from and audited against, and the application maintains no private doctrinal variant.

```text
canon/           locked source, version, SHA-256, source authority, usage terms
nature-layer/    named aspects, definitions, observable indicators, exclusions, relations
task-contracts/  accepted inputs, output schema, uncertainty and boundary behavior
training/        reviewed examples with source IDs and transformation records
evaluations/     held-out cases, adversarial variants, validators, results
```

Application inputs will contain named Nature aspects and evidence fields such as `artifact`, `source`, `timestamp`, `confidence`, and `unknowns`. Outputs will distinguish supplied evidence, inference, and unknowns; cite supplied observation IDs; and remain within the Product task contract.

## 4. Implementation phases

### Phase 0 — Define the evidence contract

Set the Product task boundary, supported observations, output schema, hardware floor, context cap, latency target, RAM budget, package-size budget, licenses, and out-of-scope behavior. Build the held-out evaluation suite before tuning.

### Phase 1 — Establish three CPU baselines

Evaluate the existing Gemma ONNX path, one comparable ONNX CPU-INT4 candidate, and one GGUF Q4_K_M candidate. A ternary candidate enters only after its Windows build path and license pass review. All candidates use the identical Canon/Nature packages, prompt template, task contracts, decoding controls, and hardware matrix.

### Phase 2 — Measure before education

For each candidate, record offline success rate, schema validity, evidence grounding, Canon/Nature consistency, unknown/conflicting-evidence handling, prompt-injection resistance, first-token latency, tokens per second, peak RAM, CPU use, and installed-package size.

### Phase 3 — Form the disposition; educate demonstrated gaps

The objective is that the Canon becomes the model's **formed disposition**, not a body of text supplied at runtime. Measure first: prompt with relevant Canon/Nature excerpts only to *evaluate* how much of the disposition a base candidate already exhibits. Where a stable gap remains across held-out cases, create a reviewed supervised-fine-tuning dataset and train a **separate LoRA adapter** — the education artifact that forms the disposition into the model while keeping base weights frozen, and the adapter hashable, reviewable, and reversible. The formed disposition is intrinsic at inference (no runtime Canon dependency); the SEED attests it was formed from the exact locked Canon. Base-model revision, source packages, training manifest, configuration, random seed, adapter hash, and evaluation report will be versioned together. Full fine-tuning and adapter merging remain later evidence-gated decisions.

### Phase 4 — Compress and regress

Only candidates that pass higher-precision behavior checks enter compression trials. Compare CPU INT4 and GGUF Q4 variants against the same held-out suite. Pruning, distillation, and ternary methods remain experimental until they demonstrate both task quality and real CPU benefit on the target hardware.

### Phase 5 — Promote a Product runtime

Promotion requires a model card, seed/dataset card, license record, SBOM, reproducible build instructions, tested hardware matrix, known-failure record, rollback target, and passing evidence report. Claims remain limited to the tested model, task, runtime, hardware, and adversarial scenarios.

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
