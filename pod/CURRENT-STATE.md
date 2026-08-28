# ProntoPC Pod — CURRENT STATE  (read fresh every resume, never cache)

This is the head of the Totem trail: what is true about the code **right now** and the
next gated step. A cold-start instance reads this, then **runs the verifier before trusting it**:

```bash
python pod/totems/verify_totems.py
```

If every Totem HOLDS, the picture below is still true. If anything is STALE/DRIFT, the code
moved since it was recorded — re-audit the affected files before acting. Never inherit a stale
picture; that is the exact drift failure this system exists to prevent.

## Head totem
**TOTEM-0009** (2026-08-28) — **Committed Pod eval suite (`tests/PodEval`) — ALL 28 checks PASS.**
Compiles the shipping `Envelope`/`GemmaLocalPlanner` and asserts 10 admissibility fixtures + 6
`Validate` checks (clamp, gb→mb, risk-stamp, catalogue-drop, false-precondition-drop, subsumption) +
3 Gemma author-path checks. Extracted `IPlanAuthor`/`PlanResult` into `PlanContracts.cs` (engine-free)
so the eval runs the real code. StoreApp build 0/0. **google/gemma-3-1b-it access now GRANTED**;
int4 model build in progress. All 9 Totems HOLD.

**Prior:** TOTEM-0008 — **Stage 2b inference code complete + building.** `OnnxGenAiInference`
(ONNX Runtime GenAI 0.15.2) implements `ILocalInference` — lazy off-thread model load, `json_schema`
guided decoding, prompt-stripped decode; `CreateOrAbsent` auto-activates the bundled Gemma via
`genai_config.json`, else `AbsentInference` (app unchanged). Package + `Assets/models/**` bundling in
csproj. WPF build 0/0. **NOT runtime-tested — the <1GB model artifact is an open decision** (no
ready-made <1GB GenAI Gemma-3-1b; ready ones ~1.9GB; on-budget needs an int4 build from
`google/gemma-3-1b-it`). All 8 Totems HOLD.

**Prior:** TOTEM-0007 — **Migrated the repair + verify phases into the Orchestrator.**
`Orchestrator.RunRepairAsync` now invokes the engine over the approved actions (progress + summary);
`MainWindow.RunRepair` is a thin view. The ctor is now non-throwing — an unkeyed/unloadable envelope
degrades ONLY the plan phase (labelled offline), leaving repair/verify working. The Orchestrator owns
the full phase machine (plan+consent+repair+verify), with cloud IO in the view adapters. Behavior-
preserving refactor; WPF build 0/0. All 7 Totems HOLD. (TOTEM-0006 built the Gemma Satellite author;
-0005 Orchestrator Stage 1; -0004 the envelope + Satellite spec; -0003 the repair-plan Orb; -0002 its
spec; -0001 opened the Pod.)

## What is true now
- Clean Pod opened at `pod/`, validates against contracts. Pod trust_state: **draft**.
- **Placed (provisional):** repair-engine, diagnostics, session, loyalty, stripe-portal, cloudrun-portal.
- **repair-plan: IMPLEMENTED** (code done, Auditor 4/4 static PASS) but trust_state still **draft**
  because **C3 is LIVE-only** and unrun. I2 is fixed in code.
- **repair-plan Satellite `local-model`: SPECCED (draft).** v0.1 = Gemma 3 1B-it QAT, on-device,
  client-side; cloud (Azure→Gemini) demoted to fallback. Not yet built — no ONNX runtime/model in
  `ProntoPC.csproj` yet. Pro tier = Gemma 4 E2B/E4B multimodal (deferred).
- **Action Envelope: BUILT + ENFORCED.** `action-envelope.json` (26 guards) + schema; the
  repair-plan Umbilical is **keyed** to it and the Orchestrator **holds** it. Now **consumed** by
  `StoreApp/Envelope.cs` at runtime (embedded via csproj Link — single source of truth). Runtime-
  proven.
- **Orchestrator: BUILT (client-side), owns the full phase machine.** Holds the envelope, version-keys
  the Umbilical, authors via provider order + validates before consent (plan), and now runs
  repair+verify (`RunRepairAsync`). `MainWindow` is a thin view (UI + cloud IO adapters). Unkeyed
  envelope degrades only the plan phase.
- **Gemma Satellite: Stage 2a BUILT.** `StoreApp/LocalPlanner.cs` — `GemmaLocalPlanner:IPlanAuthor`
  wired FIRST in provider order behind an `ILocalInference` seam (prompt + admissible-set grammar +
  prose-tolerant parse + `provenance:local-model`). `AbsentInference` default → app falls through to
  cloud unchanged. Runtime-proven with a fake inference. **Stage 2b GATED on Tracey:** Gemma weights
  + license, then `Microsoft.ML.OnnxRuntimeGenAI` + `OnnxGenAiInference` (recipe in the Satellite SPEC).
- **Draft — not built yet:** custodian-stage, llm-portal, mint-portal, **Gemma Stage 2b (on-device inference)**.
- Manifest of Completion: `docs/MANIFEST_OF_COMPLETION.md`. Opening audit: 5 PASS / 3 FAIL
  (I1, I2, U6) / 4 PARTIAL / 4 UNVERIFIABLE.
- **I2 now fixed** in `StoreApp/MainWindow.xaml.cs` (no silent non-AI substitution; offline plan is
  explicitly labelled). Still open: **C4** — "35+ categories" claim is really **25** (`Diagnostics.cs`).

## Open review items (carried, not blessed)
- **Session gate:** `handleSubmitDiagnostics` now **auto-creates** a session instead of 404 on an
  unknown token — confirm the payment gate is still enforced at repair-*execution*.
- **Uncommitted:** the repair-plan Orb impl + AEGIS rebrand are still in the working tree
  (12 modified files) — nothing committed since `b6272f8`.
- **Envelope precision:** `smart_health[]` leaf schema unconfirmed, so `chkdsk_scan`/`hardware_report`
  are gated on crash/device-error proxies; tighten once confirmed. Per-browser cache MB unbounded
  (subsumed by `clear_all_browser_caches`).

## Next gated step
**Stage 2b (needs Tracey):** accept the Gemma license + download `gemma-3-1b-it` int4 ONNX into
`StoreApp/Assets/models/`, add `Microsoft.ML.OnnxRuntimeGenAI`, implement
`OnnxGenAiInference:ILocalInference`, swap `AbsentInference`. Recipe in the Satellite SPEC §6b. Then:
offline-VM DoD (`provenance:"local-model"`, no network), grow the runtime proof into a ~10-fixture
eval suite, and migrate the repair/verify phases into the Orchestrator. (Alternative cloud-C3 path
still open: deploy to Cloud Run + `certification_preflight.py`. After: `prontopc-custodian-stage`.)

## How to work here
1. `python pod/totems/verify_totems.py` — confirm the picture holds.
2. Read the latest Totem in `pod/totems/totems.jsonl` for what was last done + why.
3. Do the next gated step. When you finish a unit of code work, **append a new Totem**
   (hash-bind the files you touched) and update this head. Append-only — never edit a past Totem.
