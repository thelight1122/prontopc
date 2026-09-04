# ProntoPC - Microsoft Store Certification Notes

Product: ProntoPC
Product ID: 9P1K9QJXKND8

Paste the block below into Partner Center -> Submission -> "Notes for certification"
when resubmitting. ProntoPC is a free download; each diagnostic + repair run is a
one-time $29 purchase via Stripe checkout. To test the full paid experience at no
cost, the reviewer applies the 100%-off promotion code below - no payment card and
no account/sign-in are required.

------------------------------------------------------------------
ProntoPC is an AI-powered Windows diagnostic and repair app. The app is a free
download; each repair run is unlocked by a one-time $29 checkout. The AI that builds
the repair plan is a model bundled inside the app and runs on the device itself, so
the repair plan is generated locally and does not depend on any internet service.

BEFORE YOU START: use promotion code  MSCERT26  at checkout (step 4). It makes the
total $0.00 and requires no payment card. There is no account and no sign-in anywhere
in this flow.

HOW TO TEST (full functionality):
1. Install ProntoPC from the Microsoft Store and launch it.
2. Approve the single Windows (UAC) prompt. ProntoPC elevates once so it can both
   scan and repair; you will not be prompted again.
3. On the "Ready to service your PC" screen, click "Start your $29 diagnostic." Your
   browser opens the secure Stripe checkout.
4. On the checkout page, click "Add promotion code" and enter:
       MSCERT26
   The total changes from $29.00 to $0.00 and NO payment card is required. Enter any
   valid email address and complete the (free) checkout.
5. Return to the ProntoPC window. It detects the completed checkout automatically
   (it is polling) and continues on its own - no code to copy, nothing to paste.
6. The app reads the system - "Reading your system..." - for about a minute. This
   phase is read-only; nothing on the PC is changed.
7. The bundled on-device AI builds a personalized repair plan (no internet connection
   is needed for this step). The app then shows "Here's what we found." with a list of
   recommended repairs and a live on-device Technician you can ask about those findings.
8. Ask the Technician a question if helpful, or uncheck any item you would rather skip,
   then click "Perform Repairs." The conversation explains findings only; it cannot perform a
   repair. Repairs run
   immediately (permission was already granted by the UAC prompt in step 2).
9. When finished, the app shows "Your PC has been serviced."

NOTES:
- Pricing: the app is a free download; each run is a one-time $29 Stripe checkout.
  There is no subscription and no in-app purchase inside the Store package - the
  charge is a standard Stripe checkout in the browser.
- No real payment is taken anywhere in this flow when the MSCERT26 code is applied,
  and no account or sign-in is required - the run is unlocked by the checkout itself.
- After checkout the ProntoPC window continues automatically; the reviewer does not
  need to return a code to the app or paste anything.
- The AI repair plan is generated on-device by a bundled model, so the plan step
  works even with no network connection.
- On-screen figure: the app shows an animated "Custodian" character at the top of the
  window that reflects the current step (reading, planning, repairing, done). It is a
  visual status indicator only - it performs no action on its own and does not generate
  any text or conversation.
- Support: support@prontopc.online
------------------------------------------------------------------

## What changed since the last submission (for our records)
Prior rejection (10.1.2.10 Functionality, Aug 26 2026): "repair plan built by AI"
was absent because BOTH cloud AI providers were down at review time (Azure OpenAI
401 + Gemini 429), and the app depended on the cloud to author the plan. (An earlier
rejection was a Stripe $0-checkout redirect bug, since fixed - a 100%-off checkout
settles as "no_payment_required"/"complete", which /api/verify-payment now accepts.)

Root cause fixed - durably:
- The repair plan is now authored on-device by an AI model bundled in the app
  (int4 google/gemma-3-1b-it via ONNX Runtime GenAI). The Orchestrator has one
  local author and no cloud fallback, so the plan is generated with no network
  dependency. The prior cloud-outage failure mode cannot recur during review:
  the reviewer's plan is authored on the device.
- If, on some machine, the on-device author cannot produce a plan, the app never
  substitutes a non-AI (rule-based) plan under the AI claim. It shows honest
  recourse instead (a make-good). There is no code path that presents a heuristic
  plan as the AI plan.

## UI & AI-governance posture in this build (for our records)
- The findings/repair flow is fronted by the Custodian actuator described above: an
  on-stage vector figure that moves through the real pipeline states (greeting, scanning,
  thinking, presenting, repairing, complete) driven only by true events - never a timer -
  and marks each real repair action as it completes. It is observation-only: it shows the
  work, it does not generate any user-facing language.
- The conversational AI "Technician" is active on the post-scan Findings screen. It runs entirely
  on-device: every inbound turn is PIM-screened, every outbound reply passes the integrity/RBC/PRISM
  boundary, and complete turns are retained in a hash-chained interaction ledger. It is grounded in
  the session findings and explains them; it never executes or authorizes a repair. **Perform
  Repairs** remains the separate explicit-consent path the reviewer exercises.

## Payment wiring (pay-per-run, for our records)
- Gate: the app runs nothing until a fulfilled checkout exists. On launch it either
  re-verifies a token it arrived with (prontopc://run?token=..., via /api/verify-token)
  or, on a Start-menu launch, shows the checkout gate.
- Checkout: "Start your $29 diagnostic" opens the Stripe Payment Link tagged with a
  per-launch nonce (?client_reference_id=...). The app polls /api/await-payment?ref=...
  until the paid session carrying that nonce appears, then continues in the same
  window. No deep-link is required, so a Start-menu launch never spawns a second
  instance or a second UAC prompt.
- A session exists server-side ONLY after Stripe confirms a fulfilled (paid or
  100%-off comped) checkout, so a poll hit is proof of payment.

## Pre-submission checklist (ours, not for the reviewer)
- [ ] The .msixupload BUNDLES the Canon-formed on-device model at
      Assets/models/gemma-3-1b-it-formed-v0.2.1/ (genai_config.json + weights; base
      and analyzer models pruned). Without it the app cannot author a repair plan
      and can only present honest make-good. This is the single most important
      packaging check.
- [ ] Confirm in the Stripe Dashboard (LIVE mode) that promotion code MSCERT26
      still exists and is 100% off, and that the $29 Payment Link's after-payment
      redirect points at the STORE continuation page
      (success-store?session={CHECKOUT_SESSION_ID}) — NOT success.html, which is the
      legacy paste-command page and must not be the post-payment landing.
- [ ] The backend (pronto-server-cloudrun.mjs) with /api/await-payment +
      /api/verify-token is deployed to Cloud Run.
- [ ] `python certification_preflight.py` reports safe to submit.
- [ ] The bundled Gemma license + Prohibited Use Policy open from the in-app
      "AI model & licenses" link.
