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
**TOTEM-0004** (2026-08-28) — **Specced the local-model Satellite + built the strict bounded
Action Envelope.** v0.1 ships an embedded **Gemma 3 1B-it QAT** (text-only, <1GB, ONNX Runtime
GenAI + DirectML, bundled in the MSIX) that authors the plan **on-device, client-side** — the
durable kill for the C3 billing cliff; Gemma 4 E2B/E4B multimodal is deferred to **Pro**. The
**Action Envelope** (`pod/orbs/repair-engine/action-envelope.json`, 26 guards grounded in
`Repair.cs`) is a per-action admissibility contract: the **repair-engine owns** it, the
**Orchestrator holds** it and **keys the plan Umbilical** to it. Safety is now structural — a 1B
model can't emit an inadmissible/mislabelled/over-claiming action. All 4 Totems HOLD (drift on the
two code files cleared by re-audit). (TOTEM-0003 implemented the repair-plan Orb; -0002 its spec;
-0001 opened the Pod.)

## What is true now
- Clean Pod opened at `pod/`, validates against contracts. Pod trust_state: **draft**.
- **Placed (provisional):** repair-engine, diagnostics, session, loyalty, stripe-portal, cloudrun-portal.
- **repair-plan: IMPLEMENTED** (code done, Auditor 4/4 static PASS) but trust_state still **draft**
  because **C3 is LIVE-only** and unrun. I2 is fixed in code.
- **repair-plan Satellite `local-model`: SPECCED (draft).** v0.1 = Gemma 3 1B-it QAT, on-device,
  client-side; cloud (Azure→Gemini) demoted to fallback. Not yet built — no ONNX runtime/model in
  `ProntoPC.csproj` yet. Pro tier = Gemma 4 E2B/E4B multimodal (deferred).
- **Action Envelope: BUILT (contract, draft).** `pod/orbs/repair-engine/action-envelope.json`
  (26 guards) + `pod/contracts/action-envelope.schema.json`; the repair-plan Umbilical is **keyed**
  to it (`working_parameters.envelope_ref`) and the Orchestrator **holds** it
  (`orchestrator.holds_contracts`). No code consumes it yet — the Orchestrator is `to_build`.
- **Draft — not built yet:** custodian-stage, llm-portal, mint-portal, **Orchestrator**.
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
**Two tracks. Preferred (durable C3): build the on-device model path.** Build the **Orchestrator**
envelope-keying + client **`ILocalPlanner`** (Gemma 3 1B via ONNX Runtime GenAI): compile the
per-machine admissible set from the envelope, constrained-decode, post-validate. This makes the AI
plan present offline and retires the billing cliff **without** a live cloud deploy.
**Alternative (cloud C3):** deploy backend to Cloud Run (us-central1) + rebuild MSIX + run
`python certification_preflight.py` (needs Tracey — gcloud creds / VS packaging). After either:
build `prontopc-custodian-stage` (the redesign).

## How to work here
1. `python pod/totems/verify_totems.py` — confirm the picture holds.
2. Read the latest Totem in `pod/totems/totems.jsonl` for what was last done + why.
3. Do the next gated step. When you finish a unit of code work, **append a new Totem**
   (hash-bind the files you touched) and update this head. Append-only — never edit a past Totem.
