# ProntoPC - Microsoft Store Certification Notes

Product: ProntoPC
Product ID: 9P1K9QJXKND8

Paste the block below into Partner Center -> Submission -> "Notes for certification"
when resubmitting. ProntoPC needs no test account and no promo code - the app is
self-contained and requires no sign-in.

------------------------------------------------------------------
ProntoPC is an AI-powered Windows diagnostic and repair app. Everything runs on
the local PC - there is no account, no sign-in, and no internet connection is
required to use the core feature. The AI that builds the repair plan is a model
bundled inside the app and runs on the device itself.

HOW TO TEST (full functionality):
1. Install ProntoPC from the Microsoft Store and launch it.
2. Approve the single Windows (UAC) prompt. ProntoPC elevates once so it can both
   scan and repair; you will not be prompted again.
3. The app reads the system - "Reading your system..." - for about a minute. This
   phase is read-only; nothing on the PC is changed.
4. The bundled on-device AI builds a personalized repair plan. No internet
   connection is needed for this step. The app then shows "Here's what we found."
   with a list of recommended repairs.
5. Uncheck any item you would rather skip, then click "Perform Repairs." Repairs
   run immediately (permission was already granted by the UAC prompt in step 2).
6. When finished, the app shows "Your PC has been serviced."

NOTES:
- No account or sign-in is required. No promotion code or external website is
  involved, and there is no separate download - the Store app is the product.
- The AI repair plan is generated on-device by a bundled model, so the core
  feature works with no network connection.
- Support: support@prontopc.online
------------------------------------------------------------------

## What changed since the last submission (for our records)
Prior rejection (10.1.2.10 Functionality, Aug 26 2026): "repair plan built by AI"
was absent because BOTH cloud AI providers were down at review time (Azure OpenAI
401 + Gemini 429), and the app depended on the cloud to author the plan.

Root cause fixed - durably:
- The repair plan is now authored by an on-device AI model bundled in the app
  (int4 google/gemma-3-1b-it via ONNX Runtime GenAI). The Orchestrator's provider
  order is on-device first, cloud only as a fallback, so the plan is generated
  locally with no network dependency. The prior cloud-outage failure mode cannot
  recur during review: the reviewer's plan is authored on the device.
- If, on some machine, the on-device author cannot produce a plan, the app never
  substitutes a non-AI (rule-based) plan under the AI claim. It shows honest
  recourse instead (a make-good). There is no code path that presents a heuristic
  plan as the AI plan.

## Pre-submission checklist (ours, not for the reviewer)
- [ ] The .msixupload BUNDLES the on-device model at
      Assets/models/gemma-3-1b-it/ (genai_config.json + weights). Without it the
      app falls through to the cloud fallback, which reintroduces the exact
      failure that caused the last rejection. This is the single most important
      packaging check.
- [ ] `python certification_preflight.py` reports safe to submit.
- [ ] The bundled Gemma license + Prohibited Use Policy open from the in-app
      "AI model & licenses" link.
