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
**TOTEM-0027** (2026-08-29) — **Re-aimed the certification gate at the ON-DEVICE product (Tracey's greenlit path).**
The Store submission is the self-contained WPF app: self-elevates once (single UAC), scans read-only, and authors
the repair plan **on-device** with the bundled int4 `gemma-3-1b-it` (Orchestrator order = local-model first, cloud
fallback only). This **durably kills the 10.1.2.10 dual-provider outage** that caused the 4th rejection: verified
LIVE that the cloud `/api/repair-plan` is **still down** (Azure **401** invalid key/endpoint + Gemini **429** prepay
depleted — the exact 4th-rejection state), yet **on-device authoring PASSES** (`tests/ModelSmoke`:
`provider==local-model`, ≥1 admissible action after Envelope validation, non-empty summary — run live this session).
(1) Rewrote `docs/MICROSOFT_STORE_CERT_NOTES.md` to the real reviewer flow (install → launch → **single UAC** →
read-only scan → **on-device AI plan** → "Perform Repairs" → "Your PC has been serviced"); removed the obsolete
Stripe $29 + `MSCERT2026` + download-a-separate-agent + Desktop-report claims (none exist in the WPF app); added a
**pre-submission checklist whose #1 item is that the `.msixupload` BUNDLES the model** (else it falls to the dead
cloud = re-rejection). (2) Retargeted `certification_preflight.py`: check[3] blocking gate is now **on-device-model-
bundled + ModelSmoke proof**, cloud **demoted to non-blocking** fallback; check[5] validates the on-device notes
instead of demanding the Stripe code. **Preflight now 18/19 → SAFE TO SUBMIT** (sole warning = cloud fallback 429,
handled by on-device + make-good). Docs + gate only — no StoreApp engine code changed. All 28 Totems HOLD.
**Open for Tracey:** monetization/pricing of the Store listing (the WPF app has no in-app payment — free vs paid-in-
Store is a Partner Center decision, not a cert blocker); optional belt+suspenders = restore a cloud provider so the
fallback is green too. **Next: Tracey builds the `.msixupload` (with the bundled model) in VS + submits (identity
`9P1K9QJXKND8`).**

**Prior:** TOTEM-0026 (2026-08-28) — **custodian-stage Phase 2: multi-turn inference plumbing built + proven (echo is
an open quality issue).** Additive — `ILocalInference.Complete` (single-shot plan authoring) untouched; **added
`ILocalInference.Chat(history)`** for free-form multi-turn, implemented in `OnnxGenAiInference` (Gemma
`<start_of_turn>` template, free-form decode) + `AbsentInference` (null). New `StoreApp/CustodianChat.cs` = the
orb's declared entrypoint (history + `ReplyAsync`; **nothing executes an action**). **Proven:** `dotnet build`
**0/0**; `tests/ModelSmoke` extended with a 2-turn on-device exchange — ran the real int4 `gemma-3-1b-it`,
history carried (4 turns), plumbing PASS. **Open (honest):** the raw 1B replies **echo the prompt** instead of
answering — root causes found in the model config: template omits `<bos>` (`bos_token_id=2`); sampling is
`temperature=1.0` vs the Conversation Contract's 0.5; placeholder system prompt (not yet Canon-seeded). **Next:
decode tuning** (add `<bos>`; apply the Conversation Contract interactive_params), then re-prove a *coherent*
2-turn exchange. (Also noted: plan authoring intermittently fails valid-JSON at temp 1.0 — pre-existing
sampling flakiness; the Phase-2 change is purely additive and doesn't touch that path.) All 26 Totems HOLD.

**Prior:** TOTEM-0025 (2026-08-28) — **Implemented + documented the AEGIS Build Invariants standard, seeded from the
real Canon.** Read `AEGIS CORE FOUNDATIONAL CANON v1.0` (Canon-Locked 2026-05-31) + the Responsible-AI spec +
Concept Ledger + integration template + Interceptor blueprint (on `I:\`/`H:\`) — not an approximation. Two new
docs: **`docs/AEGIS_BUILD_INVARIANTS.md`** — the pre-offering standard for all Si Energized Applications (non-force
spine, the locked pipeline PIM→…→Integrity→IDQRA→RBC/Reflective-Boundary-Conditions→Append-Only Lineage, the
Seven Imperatives / Four Standards / Offering gates / Seven Virtues / Responsible-AI reqs / append-only auditable
interaction log / pre-offering checklist), written in **"will" not "must"** (a *must* is a force artifact) and
referencing the external Canon as authority (Canon Boundary). **`docs/PRONTOPC_AEGIS_INTEGRATION_CONTRACT.md`** —
the Custodian filed as `officiality=variant, divergence declared`, honestly mapping what ProntoPC **already**
instantiates (Totem ledger = Append-Only Lineage; Action Envelope = Carrier Envelope; §0+Conversation Contract =
Non-Force/RBC; defer-when-uncertain = Grounding Verification; consent+envelope = Executable Boundaries; U1–U6 =
Observation-Only) vs the **declared build-gaps** (seed the Canon; build PIM; the 7-virtue Integrity screen —
gated on core, IRG scoring undesigned even in AEGIS core; RBC/Prism module; the append-only **interaction** log
— Totem covers dev lineage, not the runtime customer log). `custodian-stage` SPEC §3c binds the build to this
gate. Documentation-level implement (no engine code faked). All 25 Totems HOLD.

**Prior:** TOTEM-0024 (2026-08-28) — **Captured the Custodian conversation choreography (SPEC §3b).** Tracey's
greenlit flow: the AI Technician is the **through-line narrator** across the whole repair, with a yes/no
question-gate at every boundary (No→proceed, Yes→chat then re-offer — the customer always chooses the step,
= §0 in motion). Journey: pre-scan brief+gate → reconnaissance → findings+intended-actions+gate → repairs
(existing consent/envelope path; chat never executes) → **Final Audit & Certification** (Technician *verifies*
repairs actually completed — real per-action check grounded in the Orchestrator's existing verify output,
certifies only what truly completed, U2/U3) +gate → **Technician personally presents the 50% coupon**
(`NEXTREPAIR50`) as relationship-building, not a page element. Build phases revised; contract gained
`gate_shown`/`certified`/`coupon_presented` events. **Still SPEC-only — first build unit is Phase 2
(multi-turn inference).** All 24 Totems HOLD.

**Also this session (Tracey's gates):** backend **A1 deploy DONE** (Cloud Run rev `pronto-pc-00075-pkz`,
100% traffic, make-good code live) + **A2 verified** (`/health` 200; `/api/report-ai-unavailable` → 400 wired).

**Prior:** TOTEM-0023 — **Founding principle set atop the web spec (§0):** the product never projects
authority/force/dominance onto the customer; every other rule derives from it. TOTEM-0022 — banned redundant
doubt-planting reassurances ("at no additional cost") in the web spec's honesty checklist.

**Prior:** TOTEM-0021 (2026-08-28) — **Greenlit web-surface reference spec drafted.** `docs/WEB_SURFACE_SPEC.md` is
the source of truth the web rebuild is authored **against** (Tracey's clean-rebuild rule: new files by
reference, never edited clones). Holds the one true product model (certified Store app, no copy-paste,
on-device conversational Custodian, read-only consent-gated scan, six areas, $29, honest make-good), the true
customer journey (retires the paste flow), the page inventory to author fresh, the **anti-fossil honesty
checklist** (banned claims / required truths, tied to C3/C4/I2/U1–U6), the mascot-voice rule, the AEGIS design
tokens, and proven wiring to *reference* not clone. **Brand direction captured:** the Custodian mascot is the
**Brand Ambassador** — featured at prominent scale in hero + closing CTA, never a small icon (the current
clone renders him too small; the rebuild sizes him right from the start). Reference only — no page rebuilt yet.
All 21 Totems HOLD.

**Prior:** TOTEM-0020 (2026-08-28) — **Killed another paste fossil + mapped the rest.** FAQ "Do I need technical
knowledge?" still said *"The only technical action is pasting one command"* (missed by TOTEM-0018's sweep —
pattern didn't match "past**ing**"). Fixed to the certified-app truth (the app runs the diagnostic itself; no
commands to type or paste). Exhaustive sweep: `index.html` is now **clean**. The *"nothing is installed/
downloaded/runs in memory"* claim Tracey flagged is **already gone from the repo** (removed TOTEM-0018) — the
quote is from the **stale live site**, so a **Vercel redeploy** is still needed. **Remaining paste fossils are
in FUNCTIONAL flows, deliberately NOT edited** (would break real Stripe-wired delivery before the certified-app
download flow is designed): `success.html` (post-payment Run-box/PowerShell/Ctrl+V scan+repair) and
`guardian.html`/`guardian-offer.html` (the subscription is also all-paste). Recorded Tracey's **relational-DB-MCP
knowledge-lookup** option in `custodian-stage` SPEC §6 (Phase-4 fork vs. system-prompt prose). All 20 Totems HOLD.

**Prior:** TOTEM-0019 (2026-08-28) — **Opened the `custodian-stage` Orb — the conversational Custodian Technician
(SPEC + Conversation Contract, no app code).** The on-device Gemma stops being a silent one-shot plan author
and becomes a **session-scoped chat technician** — the chat window that replaces the command window
(Tracey's direction; settles the A/B as **A, the certified app**). `custodian-stage` was already declared
draft in `pod.json` (specs U1–U6) but its dir was empty and its umbilical dangled — now filled. Core artifact:
the **Conversation Contract** (Tracey's "Model Contract") — a JSON schema + instance, **sibling of the Action
Envelope** (envelope bounds what the model may *do*; this bounds how it may *talk*): fixes interactive params
(temperature/top_p/max_new_tokens/max_turns), scopes talk to one repair session, and states the guardrails —
may **explain** an action but never author/approve/execute it (handoff to the envelope-validated consent path =
continuation of **I2**), defer-when-uncertain (**U2**), no credentials, injection-resistant. Curated Windows-repair
reference (prose Markdown, grounded in the six areas + the real 26 actions) ingested into the **32k** context —
no RAG for v0.1. **Build phases next (each its own Totem):** (2) multi-turn `ILocalInference`, (3) `ChatPanel`,
(4) system-prompt ingestion. JSON validated; all 19 Totems HOLD.

**Prior:** TOTEM-0018 (2026-08-28) — **Killed a copy-paste architecture drift in the site "how it works."**
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
**Product decided (TOTEM-0027): the Store submission is the ON-DEVICE self-contained WPF app.** The cert gate is
green — `certification_preflight.py` reports **18/19, SAFE TO SUBMIT** against the live system, with on-device
authoring proven this session. The remaining steps are Tracey's:
1. **Build the `.msixupload` in VS** — identity `9P1K9QJXKND8` already set. **CRITICAL:** confirm the package
   BUNDLES the on-device model (`StoreApp/Assets/models/gemma-3-1b-it/`, ~700 MB, gitignored artifact — present in
   the working tree now). Without it the app falls to the dead cloud = the exact 4th-rejection failure.
2. **Submit in Partner Center** with the rewritten `docs/MICROSOFT_STORE_CERT_NOTES.md` in "Notes for certification."
3. *(Optional belt+suspenders)* restore a cloud provider (valid Azure key/endpoint, or Gemini prepay credits) so the
   fallback + make-good path is green too — not a cert blocker, since on-device authors the reviewer's plan.
4. *(Open, Tracey's call)* Store-listing monetization: the WPF app has no in-app payment — free vs paid-in-Store is a
   Partner Center pricing decision, independent of cert. The web surface (`prontopc.online` + Stripe) still exists.
On cert pass, promote repair-plan **provisional → verified**. After: `prontopc-custodian-stage` (Phase-2 echo fix).

Watch item (not blocking): the 1B model's summary specificity was low on the WPF run (generic wording,
1 action, ~0 MB) — consider prompt/decoding tuning if it recurs on a machine with real clutter.

## How to work here
1. `python pod/totems/verify_totems.py` — confirm the picture holds.
2. Read the latest Totem in `pod/totems/totems.jsonl` for what was last done + why.
3. Do the next gated step. When you finish a unit of code work, **append a new Totem**
   (hash-bind the files you touched) and update this head. Append-only — never edit a past Totem.
