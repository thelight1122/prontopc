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
**TOTEM-0018** (2026-08-28) — **Killed a copy-paste architecture drift in the site "how it works."**
Tracey caught step 3 describing a **copy-paste-a-command** flow ("single command to paste… Nothing is
installed") — which is **not the product**. The shipping product is the **Microsoft-Store-certified**
Custodian app that self-elevates (UAC) and runs the scan itself: **no command for the customer to copy or
paste** (and Store cert = no purchased code-signing cert). Reworded step 3 + the "25 diagnostic categories"
claim (which traced to the *unshipped* `Diagnostics.cs`, not the live 29-field web scan) to the **six
headline areas** (Tracey's call) across `index.html` (×3), `guardian-offer.html`, `terms.html`.
**⚠ Bigger drift flagged, NOT edited:** `success.html` + `pronto-server-cloudrun.mjs` **still implement the
paste-a-command flow** (Copy Command / Win+R / scan+repair command URLs) and the server prompt still says
"35+ categories" ×2 — they contradict the certified-app product and need Tracey's direction. All 18 Totems HOLD.

**Prior:** TOTEM-0017 (2026-08-28) — **Fix It Now now breathes — a "push me" invitation.** Added `@keyframes
ppFixPulse` (soft iris halo: ring 0→7px + glow 16→40px, `#7c5cff`/`#8b6bff` low-alpha, + 1.5% scale,
2.8s ease-in-out infinite) to **both** Fix It Now CTAs — the desktop hotspot `.repair` (halo over the
baked hero card) and the mobile `.primary` button. Slow + low-amplitude by design = **confident
invitation, never a directive** (the mascot-voice rule). Honors `prefers-reduced-motion`. Wiring verified
via computed styles; live breath not frame-captured (preview pane is `document.hidden`, Chrome pauses
animations — env limit, not a bug). All 17 Totems HOLD.

**Prior:** TOTEM-0016 (2026-08-28) — **Repo review + drift cleared.** On resume the verifier flagged one DRIFT:
`index.html` (authority TOTEM-0015). Root cause is benign — commit `f55fc8c` (hero CTA redesign: single
Start-a-Repair CTA + Fix It Now card + new hero image) moved `index.html` forward **outside a Totem cycle**,
so the TOTEM-0015 hash went stale. Audited it: a hero-section change (12 ins / 15 del + base64 asset data),
**not a regression**. Re-verified the C4 honesty fix **survived** — `index.html` still reads "25 diagnostic
categories" (×2) and **no genuine "35+" claim remains** (the only "35+" byte-matches are inside the base64
image blobs, not visible copy). Re-bound `index.html` at its current hash; **all 16 Totems now HOLD**.
No code authored — a review + re-record only. External gates (below) unchanged.

**Prior:** TOTEM-0015 — **C4 fully fixed across the live site + legal pages.** Corrected every visible
"35+ categories" claim to the honest **25** in `index.html` (×4), `guardian-offer.html`, `terms.html`.
Flagged the **stale Vercel deploy** (serves a pre-AEGIS page) — a redeploy is required for the corrected
copy to go live (Tracey's step).

**Prior:** TOTEM-0014 (2026-08-28) — **Ship-readiness reached; `prontopc-repair-plan` PROMOTED draft → provisional.**
Bundled the Gemma license + Prohibited Use Policy in-app (`StoreApp/Assets/licenses/*` as Content, opened
via an in-app "AI model & licenses" link). **Verified the AI-unavailable make-good END-TO-END** in a
forced-fallback run: model disabled + cloud pointed at a keyless local server → local declines → cloud
declines → app POSTs `/api/report-ai-unavailable` (machine `DMTAI-DEV`, correct reason, coupon none) →
honest make-good screen; temp instruments reverted, build clean. Updated BUILD_INSTRUCTIONS (bundled
model+licenses, AI-transparency listing copy). Promoted repair-plan in `pod.json` + `orb.json` — C3 met
on-device makes the old "LIVE-only" gate obsolete. WPF 0/0; PodEval 28 PASS. All 14 Totems HOLD.

**Prior:** TOTEM-0013 — **Rule-based fallback removed; honest AI-unavailable recourse added.**
The app no longer shows a non-AI plan under the AI claim under any condition. When BOTH AI authors
decline (on-device Gemma AND cloud broker — now rare), the client POSTs the miss to a new backend
endpoint `POST /api/report-ai-unavailable`, which issues a **single-use 100% make-good coupon** (tagged
to that customer + that machine), **emails the customer the code**, and **notifies Support@ProntoPC.online**;
the app then shows an honest make-good screen. Deleted `BuildLocalPlan`/`ShowOfflinePlan`/`_offline` and
the dead `Orchestrator.Sanitize`. WPF build 0/0; PodEval 28 PASS; `node --check` clean; endpoint
smoke-tested locally (missing token→400, unknown token→200 ok/couponCode null). **Not deployed; no live
email/coupon sent.** All 13 Totems HOLD.

**Prior:** TOTEM-0012 — **C4 false claim killed.** The listing copy in `docs/custodian-spec.md`
overstated the diagnostic as "35+ system areas"; the honest count is **25** (verified: 25 `Section(...)`
collectors in `Diagnostics.cs`). Corrected the copy to "25 system areas" and marked C4 RESOLVED in
`pod/README.md`. No code path changed — an honesty fix to shipping listing copy. All 12 Totems HOLD.

**Latest verification (2026-08-28, no code change — instrument reverted, not a Totem):**
**FULL WPF END-TO-END RUN PROVEN with the model bundled.** Built `ProntoPC.csproj` Release (0/0), the
~700 MB int4 model + `onnxruntime-genai` natives copied into the output, launched the real exe → UAC
self-elevation → auto-scan (read-only WMI) → **the on-device Gemma authored the plan in the actual UI**
(`provider=local-model`, `model=gemma-3-1b-it`; captured via a temporary `%TEMP%` provenance trace, since
reverted). The Findings/consent screen rendered *"Here's what we found."* + the AI summary + a validated
action + "Perform Repairs", with **no "OFFLINE BASIC PLAN" banner** — the C3 kill demonstrated in the
shipping UI, not just `tests/ModelSmoke`. Stopped at consent (no repairs executed); app closed clean;
tree clean, 12 Totems HOLD. **Quality note:** this run's summary was generic and surfaced only 1 action
at ~0 MB (clean machine and/or vaguer 1B cold-run summary vs. the number-citing ModelSmoke run) — a
specificity item to watch, not a failure.

**Prior:** TOTEM-0011 — **Envelope now owns the reclaim estimate.** For measurable reclaim
actions, `Envelope.cs` **sets** `estimated_mb_recovered` to the true diagnostics measure
(`cap>0 ? round(cap) : 0`) instead of trusting the model and only clamping over-claims — so it now
also corrects the model's **under-claims upward** (the optional follow-up TOTEM-0010 flagged: the
model under-estimated temp/Windows.old). Unmeasurable actions report 0 rather than an unverifiable
guess. Paired eval fixture flipped `999999`→`5` to prove upward correction; `tests/PodEval` **28
checks PASS**. All 11 Totems HOLD.

**Prior:** TOTEM-0010 — **ON-DEVICE INFERENCE PROVEN — the durable C3 kill.** Built int4
`google/gemma-3-1b-it` (**~700 MB, under budget**) and ran it through `OnnxGenAiInference` +
`GemmaLocalPlanner`: a genuine plan **referencing the real diagnostics, offline,
`provenance.provider=local-model`**, ~29s cold CPU; envelope validated it. This kills the 10.1.2.10
billing cliff without any cloud deploy. Fixed the Gemma chat-template wrap; committed self-skipping
`tests/ModelSmoke` (PASS). Build recipe (transformers==4.57.1 pin) in the Satellite SPEC §6c.

**Prior:** TOTEM-0009 — Pod eval suite (`tests/PodEval`), 28 checks PASS.
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
- **repair-plan: PROVISIONAL** (TOTEM-0014). C3 satisfied **on-device**, so the old "C3 is LIVE-only"
  gate is obsolete; I2 hardened (no non-AI substitution at all); licenses bundled; packaging-ready.
  `verified` still pending Tracey's production deploy + hot run + external Codex audit + Store cert.
- **repair-plan Satellite `local-model`: BUILT + PROVEN (provisional).** v0.1 = int4 `gemma-3-1b-it`,
  on-device, client-side; cloud (Azure→Gemini) demoted to fallback. `Microsoft.ML.OnnxRuntimeGenAI`
  0.15.2 + the model are bundled in `ProntoPC.csproj`; proven both by `tests/ModelSmoke` and a full
  WPF end-to-end run (see Latest verification). Pro tier = Gemma 4 E2B/E4B multimodal (deferred).
- **Action Envelope: BUILT + ENFORCED.** `action-envelope.json` (26 guards) + schema; the
  repair-plan Umbilical is **keyed** to it and the Orchestrator **holds** it. Now **consumed** by
  `StoreApp/Envelope.cs` at runtime (embedded via csproj Link — single source of truth). Runtime-
  proven.
- **Orchestrator: BUILT (client-side), owns the full phase machine.** Holds the envelope, version-keys
  the Umbilical, authors via provider order + validates before consent (plan), and now runs
  repair+verify (`RunRepairAsync`). `MainWindow` is a thin view (UI + cloud IO adapters). Unkeyed
  envelope degrades only the plan phase.
- **Gemma Satellite: BUILT + PROVEN on-device.** `GemmaLocalPlanner` (author) + `OnnxGenAiInference`
  (ONNX Runtime GenAI 0.15.2, json_schema-guided, Gemma chat template) run the int4 model bundled at
  `StoreApp/Assets/models/gemma-3-1b-it` (~700 MB, gitignored build artifact). Proven offline with
  `provenance:local-model` by `tests/ModelSmoke` PASS **and by a full WPF end-to-end run in the real UI**
  (2026-08-28 — provider=local-model in the shipping app, no offline banner). `CreateOrAbsent`
  auto-activates it; falls through to cloud if absent. **C3 satisfied on-device.**
- **Draft — not built yet:** custodian-stage, llm-portal, mint-portal. **Ship readiness for repair-plan:**
  ✅ WPF end-to-end run · ✅ AI-unavailable make-good verified end-to-end · ✅ Gemma license + Prohibited
  Use Policy bundled in-app · ✅ packaging inputs verified (model + licenses as Content; manifest identity
  set) · ✅ **promoted draft → provisional**. MSIX `.msixupload` build is Tracey's VS/Partner Center step.
- Manifest of Completion: `docs/MANIFEST_OF_COMPLETION.md`. Opening audit: 5 PASS / 3 FAIL
  (I1, I2, U6) / 4 PARTIAL / 4 UNVERIFIABLE.
- **I2 now fixed** (TOTEM-0013 hardened it): there is **no non-AI substitution at all** — the rule-based
  plan was removed. When AI is unavailable the app shows honest recourse (100% make-good coupon), never
  a heuristic plan under the AI claim. **C4 now FULLY fixed:** the "35+" claim corrected to the honest
  **25** in `docs/custodian-spec.md` (TOTEM-0012) **and across the live site + legal pages** — `index.html`
  (×4), `guardian-offer.html`, `terms.html` (TOTEM-0015). **Note:** the live Vercel deploy is STALE (still
  shows a pre-AEGIS page); a **redeploy to Vercel is needed** for the corrected repo copy to go live.
- **AI-unavailable make-good: BUILT + VERIFIED end-to-end (not deployed).** `POST /api/report-ai-unavailable` in
  `pronto-server-cloudrun.mjs` mints a single-use **100% coupon** (email+machine tagged, `source=ai_unavailable`),
  emails the customer + Support@ProntoPC.online; the WPF client reports the miss and shows a make-good
  screen. Reuses the proven Resend + Stripe promo-code path. No live email/coupon sent from dev.

## Open review items (carried, not blessed)
- **Session gate:** `handleSubmitDiagnostics` now **auto-creates** a session instead of 404 on an
  unknown token — confirm the payment gate is still enforced at repair-*execution*.
- **Uncommitted:** the repair-plan Orb impl + AEGIS rebrand are still in the working tree
  (12 modified files) — nothing committed since `b6272f8`.
- **Envelope precision:** `smart_health[]` leaf schema unconfirmed, so `chkdsk_scan`/`hardware_report`
  are gated on crash/device-error proxies; tighten once confirmed. Per-browser cache MB unbounded
  (subsumed by `clear_all_browser_caches`).

## Next gated step
**`prontopc-repair-plan` is PROVISIONAL — the engineering ship-readiness is done. The remaining gates are
Tracey's, before Store cert:**
1. **Deploy** the backend (Cloud Run) with the make-good env set: `SUPPORT_EMAIL`, `MAKEGOOD_COUPON_ID`,
   `RESEND_API_KEY`, `STRIPE_SECRET_KEY` (last two already in use).
2. **Production hot run** — full end-to-end against the live deploy (success path + a real make-good).
3. **External Codex code audit.**
4. **Resubmit to Store cert** — build the `.msixupload` in VS (identity already set), submit in Partner Center.
On cert pass + hot-run confidence, promote repair-plan **provisional → verified**. After: `prontopc-custodian-stage`.

Watch item (not blocking): the 1B model's summary specificity was low on the WPF run (generic wording,
1 action, ~0 MB) — consider prompt/decoding tuning if it recurs on a machine with real clutter.

## How to work here
1. `python pod/totems/verify_totems.py` — confirm the picture holds.
2. Read the latest Totem in `pod/totems/totems.jsonl` for what was last done + why.
3. Do the next gated step. When you finish a unit of code work, **append a new Totem**
   (hash-bind the files you touched) and update this head. Append-only — never edit a past Totem.
