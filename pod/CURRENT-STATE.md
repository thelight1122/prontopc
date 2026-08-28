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
**TOTEM-0005** (2026-08-28) — **Built Orchestrator Stage 1: the client now holds the Action
Envelope and keys the plan Umbilical to it.** `StoreApp/Envelope.cs` (loader + predicate evaluator +
`AdmissibleSet` + `Validate`) and `StoreApp/Orchestrator.cs` (provider-order authors, version-key
check, validate-before-consent) added; `MainWindow` delegates the plan phase to the Orchestrator.
The cloud plan is now envelope-validated before consent — `risk`/`requires_restart` stamped from the
envelope, `estimated_mb` clamped, out-of-catalogue/false-precondition/subsumed actions dropped.
**Proven at runtime** (scratchpad console over the real `Envelope.cs`). WPF build 0/0. All 5 Totems
HOLD. (TOTEM-0004 wrote the envelope + Satellite spec; -0003 the repair-plan Orb; -0002 its spec;
-0001 opened the Pod.)

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
- **Orchestrator: Stage 1 BUILT (client-side).** Holds the envelope, version-keys the Umbilical,
  authors via provider order (Stage 1 = cloud broker), validates before consent. `MainWindow`
  delegates the plan phase. **Stage 2 pending:** the Gemma local-model Satellite as first author.
  Repair/verify phases not yet migrated into it.
- **Draft — not built yet:** custodian-stage, llm-portal, mint-portal, **Gemma Satellite (Stage 2)**.
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
**Stage 2 — attach the Gemma 3 1B local-model Satellite** as the first `IPlanAuthor` (ONNX Runtime
GenAI + DirectML; model bundled in the MSIX; grammar constrained to the Orchestrator's admissible
set). This makes the AI plan present offline and is the durable C3 billing-cliff kill — no live
cloud deploy required. Then: grow the runtime proof into a ~10-fixture eval suite; migrate the
repair/verify phases into the Orchestrator. (Alternative cloud-C3 path still open: deploy to Cloud
Run + `certification_preflight.py` — needs Tracey. After: `prontopc-custodian-stage`.)

## How to work here
1. `python pod/totems/verify_totems.py` — confirm the picture holds.
2. Read the latest Totem in `pod/totems/totems.jsonl` for what was last done + why.
3. Do the next gated step. When you finish a unit of code work, **append a new Totem**
   (hash-bind the files you touched) and update this head. Append-only — never edit a past Totem.
