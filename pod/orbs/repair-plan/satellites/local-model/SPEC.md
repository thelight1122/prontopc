# Satellite Spec — `local-model` (serves `prontopc-repair-plan`)

**Status:** draft (spec-only; not yet built). **Model decided:** Gemma 3 1B-it QAT (Q4_0) for
v0.1; Gemma 4 E2B/E4B multi-model deferred to Pro (§2 roadmap split).
**Serves:** `prontopc-repair-plan` Orb, one level down (README "Satellite" row).
**Kills, durably:** the billing-cliff root cause behind cert failure **10.1.2.10 / C3** — an
on-device model means the AI plan is *always genuinely present*, offline, with no billable
provider and no backend dependency. It also settles **I3** (llm-portal resilience): the local
model is the primary, so "all cloud providers down" no longer means "no AI."

---

## 1. Why a Satellite, and why it moves inference to the client

Today the repair-plan Orb runs as an **http_service in the Node backend** (`handleRepairPlan`),
which calls Azure → Gemini. That is the exact shape that failed certification: when both cloud
providers returned 401/429, there was no model, and the client fell back to `BuildLocalPlan`.

The embedded model does not remove the billing cliff if it sits behind the same backend — it
removes it **only when it runs on the device the app is already on.** So this Satellite is, by
design, **client-side**: it loads a quantized Gemma into the WPF process and authors the plan
locally. The cloud Orb becomes a *fallback and telemetry path*, not the critical path.

That is the one architectural pivot this spec asks you to accept:

```
BEFORE  client ──diagnostics──▶ backend ──▶ Azure/Gemini ──▶ plan     (fails when cloud is down/unpaid)
AFTER   client ──▶ [local Gemma on-device] ──▶ plan                    (always present, offline)
              └── on local failure only ──▶ backend ──▶ Azure/Gemini   (fallback)
```

`provenance.provider` becomes `"local-model"` on the happy path — a value the Umbilical
`output_contract` already permits (`"local-model | azure-openai | gemini"`). No contract change
to the Orb is required; the Satellite simply satisfies the first entry in `provider_order`.

## 2. Model selection

The task is narrow: read a diagnostics blob (mostly disk / temp / cache / update-leftover
numbers) and select actions **from a fixed 26-item catalogue** (`REPAIR_ACTION_CATALOGUE`).
This is constrained classification + short JSON authoring, **not** open reasoning and **not**
multimodal. That is why a sub-1GB text model is sufficient — most repair is deleting clutter, and
the model's job is matching diagnostics to an allow-list, not inventing repairs.

### Candidates (verified Aug 2026)

| Model | Quant | On-disk | Modality | Fits <1GB? | Notes |
|---|---|---|---|---|---|
| **Gemma 3 1B-it** | QAT **Q4_0** | **~1.0 GB** | text, 32K ctx | ✅ (at the line) | QAT holds quality near BF16; ~0.5 GB weights in VRAM. **Recommended.** |
| Gemma 3 1B-it | Q4_0 (non-QAT) | ~0.72 GB | text, 32K | ✅ (headroom) | Smaller; slightly lower quality than QAT. Viable if the QAT build crowds the budget. |
| Gemma 4 **E2B**-it | int4 Q4_0 | **2.84 GB** | text+vision+audio, **tool-calling** | ❌ (~3× over) | The Android/iOS edge line you flagged. Great model, wrong budget. Upgrade path only. |
| Gemma 4 **E4B**-it | int4 Q4_0 | >4 GB | multimodal, tool-calling | ❌ | Out of scope for an embedded ship. |

**DECIDED (Tracey, 2026-08-28): Gemma 3 1B-it QAT (Q4_0) for v0.1.** It is the only
instruction-tuned Gemma that honors the <1GB line, it is text-only (we need nothing else), 32K
context easily holds a diagnostics blob, and QAT keeps accuracy close to full precision. For a
one-off "clean the clutter" repair, the small model is sufficient. This is the shipping model for
v0.1 — no further model decision is open.

### Roadmap split — v0.1 vs Pro

- **v0.1 (this spec):** single small text model — **Gemma 3 1B-it QAT**. One model, one job:
  diagnostics → catalogue selection. Ships in the MSIX.
- **Pro (future, separate build):** an **on-device multi-model** stack — the Gemma 4 **E2B/E4B**
  edge line (2.8GB+, native tool-calling + vision/audio) so the product can, e.g., read a
  screenshot/photo of a broken screen and reason across modalities. That is a deliberately larger
  build, gated to the Pro tier where the size/RAM cost is justified. **Do not pull E2B into v0.1.**
  The `provider_order` and `provenance` contracts are already shaped to accept a swapped/added
  local provider, so Pro attaches without reworking the Orb.

## 3. Runtime selection (Windows / MSIX / .NET)

This is a WPF app shipped as MSIX (x64 + ARM64), so the runtime must be a first-class .NET
citizen that packages as in-box native DLLs.

| Runtime | .NET story | Accel | Verdict |
|---|---|---|---|
| **ONNX Runtime GenAI** (`Microsoft.ML.OnnxRuntimeGenAI`) | first-class NuGet, Microsoft-owned | **DirectML** (any Win GPU) + CPU fallback | **Recommended.** Microsoft-backed helps Store cert; DirectML runs on whatever GPU is present and degrades to CPU. Gemma is published in ONNX. |
| LLamaSharp (llama.cpp bindings) | mature NuGet | CPU / CUDA / Vulkan | Strong fallback. Consumes GGUF directly; use if ONNX Gemma weights lag a release. |
| MediaPipe LLM Inference / litert-lm | Google, mobile-first | — | This is what makes the model great *on Android/iOS*; weak Windows/.NET story. Not for this ship. |

**Recommendation: ONNX Runtime GenAI + DirectML**, Gemma 3 1B in ONNX int4. LLamaSharp+GGUF is the
named fallback. (If E2B is chosen and no ONNX build exists yet, ship via LLamaSharp+GGUF instead.)

### Constrained decoding — driven by the Orchestrator's keyed envelope

A 1B model needs a structural guarantee that it emits **valid, admissible actions as valid JSON**.
The grammar is **not** self-derived by the Satellite — it is compiled from the **per-machine
admissible action set the Orchestrator hands across the keyed Umbilical**
(`pod/orbs/repair-engine/action-envelope.json`; see the Orchestrator's `holds_contracts` and the
Umbilical `envelope_ref`). The Orchestrator evaluates each action's `precondition` against the live
diagnostics and passes only the actions that hold; the Satellite restricts `action.id` to *that*
set (not all 26) and forces the plan JSON shape.

So on a machine with no `Windows.old`, `remove_windows_old` is **not in the grammar** and the model
cannot decode it. This is **belt-and-suspenders with the envelope post-guard**: the grammar makes
admissible output the *only* output; the Orchestrator's post-validation still drops anything out of
bounds (bad id, false precondition, missing `required_param`, over-cap `estimated_mb`) and fixes
`risk`/`requires_restart` from the envelope. Catalogue AND admissibility integrity are enforced
twice, on-device — identically to the cloud author, because both are keyed to the same envelope.

## 4. Packaging & size

- **Bundle the model in the MSIX**, not a first-run download. The entire point is that the AI is
  *present* — a first-run CDN fetch reintroduces a "no AI right now" window and a network
  dependency, which is what cert flagged. In-package = guaranteed present, offline, at install.
- Cost: ~1GB added to the package. Acceptable for a repair tool; note it in the Store listing.
- **Arch:** ship an MSIX **bundle** (per-arch x64/ARM64 packages). ONNX Runtime GenAI has ARM64
  builds. The `.onnx` weights are arch-neutral (they can be shared or duplicated per package —
  measure both against Store size limits).
- Model file lives under `StoreApp/Assets/models/gemma-3-1b-it-int4/` and is added to the csproj
  as packaged `Content`.

## 5. Client contract (C# side)

Introduce `ILocalPlanner` in the WPF app — the on-device mirror of the backend `authorRepairPlan`:

```
interface ILocalPlanner {
    // returns { plan, provenance:{ provider="local-model", model } } or null if the model
    // could not author a valid plan (then the client falls through to the cloud Orb).
    Task<LocalPlanResult?> AuthorAsync(Diagnostics diag, CancellationToken ct);
}
```

Obligations (mirror the Umbilical `output_contract` on the client):
1. Load the ONNX model once (lazy, cached); warm on first scan.
2. Build the prompt from real diagnostics (reuse the backend's `buildDiagnosticText` wording so
   provider parity is exact).
3. Constrained-decode a plan whose `action.id`s ∈ the **Orchestrator-supplied admissible set**
   (envelope `precondition` × live diagnostics), not the raw 26.
4. Return the plan to the Orchestrator, which post-validates against the keyed envelope (drop + log
   strays, clamp `estimated_mb`, stamp `risk`/`requires_restart`) — identically to the cloud author.
5. Return `provenance = { provider:"local-model", model:"gemma-3-1b-it-int4" }`.
6. On any failure (load error, timeout, empty plan) return `null` — **never** a heuristic plan
   wearing the AI plan's clothes. The client's existing `status`-branch then tries the cloud Orb,
   and only if that is also `unavailable` does the explicitly-labelled `ShowOfflinePlan`
   (`BuildLocalPlan`) appear. I2's structural kill is preserved end-to-end.

`provider_order` on the client becomes: **local-model → (cloud Orb: Azure → Gemini) → labelled
offline plan.** The Orb's own `orb.json` `provider_order` already names this sequence.

## 6. Definition of Done

**→ Satellite attached (repair-plan promotable toward provisional) when all hold:**
- **Present:** the ONNX model ships inside the MSIX; a clean-install VM with **no network** runs a
  scan and receives an `ok` plan with `provenance.provider == "local-model"`.
- **Catalogue integrity:** every `action.id` the local planner can emit ∈ the 26-item catalogue,
  proven by constrained decoding **and** the post-filter (two mechanisms).
- **References diagnostics:** the offline plan cites the machine's real numbers
  (disk/temp/update-leftovers), satisfying `certification_preflight.check_ai_repair_plan`'s intent
  without any cloud call.
- **I2 preserved:** an Auditor read finds the local path returns a real plan or `null` → cloud →
  labelled offline; no non-AI plan is ever presented as AI.
- **Latency budget:** cold author ≤ ~10s, warm ≤ ~3s on a mid-range CPU-only laptop (DirectML
  faster). If unmet on the floor hardware, revisit quant or model.
- **License:** the **Gemma Terms of Use** + Prohibited Use Policy are bundled/linked in-app and in
  the Store listing, and Google attribution is present. (Gemma license permits commercial
  bundling; it is not OSI-open and carries pass-through obligations — legal must confirm before
  ship.)

## 6b. Build status (Stage 2a done; Stage 2b gated on the weights)

**Stage 2a — DONE (`StoreApp/LocalPlanner.cs`, wired first in provider order).** `GemmaLocalPlanner :
IPlanAuthor` builds the prompt (native port of the backend diagnostic wording), builds a JSON-schema
grammar restricting `action.id` to the Orchestrator's admissible set, calls inference behind the
`ILocalInference` seam, extracts prose-wrapped JSON, and returns `provenance:"local-model"`.
`AbsentInference` (the default) declines, so the app currently falls through to the cloud broker
unchanged. Proven at runtime with a fake inference: prose-wrapped JSON parsed → `local-model` →
envelope-validated (risk stamped, mb clamped, false-precondition dropped).

**Stage 2b — GATED on Tracey (weights + license), then one class:**
1. Accept the Gemma license on the model page (gated repo), then download the 1B int4 ONNX
   (DirectML) build with the current `hf` CLI (`huggingface-cli` was renamed to `hf` on 2025-07-25):
   `pip install -U huggingface_hub` → `hf auth login` →
   `hf download onnxruntime/Gemma-3-ONNX --include "gemma-3-1b-it/gpu/gpu-fp16-io-int4-rtn-block-32/*" --local-dir StoreApp/Assets/models/`
   (or the `cpu/*` build). Bundle under `StoreApp/Assets/models/gemma-3-1b-it/` as packaged `Content`.
2. csproj: `<PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI" Version="0.15.*" />`
   (or the `.DirectML` variant).
3. Implement `OnnxGenAiInference : ILocalInference` — `new Model(dir)` / `new Tokenizer(model)`;
   `Available` = model dir present + loaded; `Complete` = encode prompt → `GeneratorParams` (set
   `max_length`; apply the JSON-schema via the runtime's guided-decoding/guidance grammar) →
   `Generator` loop → detokenize. Swap `new AbsentInference()` → `new OnnxGenAiInference(modelDir)`
   in `MainWindow`.
4. DoD: offline VM, no network → `ok` plan with `provenance.provider == "local-model"`; latency
   budget; Gemma license bundled/linked in-app + Store listing.

## 7. Contracts to add on approval
- `satellite.json` — the Satellite descriptor (id `prontopc-repair-plan-local-model`, `serves_orb`,
  runtime `csharp / in-process`, model + runtime pins, provenance value it supplies). Needs a
  small `satellite.schema.json` in `pod/contracts/` first — deliberately **not** fabricated here.
- csproj change bundling the model as `Content` + the ONNX Runtime GenAI PackageReference.
- A Totem appended when the first code lands, hash-binding the csproj + `ILocalPlanner`.
