# ProntoPC — Microsoft Store Certification Notes

Product: ProntoPC
Product ID: 9P1K9QJXKND8

Paste the block below into Partner Center → Additional Testing Information / Notes for certification
when resubmitting.

Partner Center settings to match this model:
- **Pricing**: set a one-time **base price of $9.99** (or nearest tier). This is a **paid app**; the
  purchase is the Store price tier.
- **Product declarations**: the app does **not** offer in-app purchases and does **not** use any
  third-party/external commerce. The only purchase is the Store base price (Microsoft Store commerce).
- **Listing metadata (10.8.4)**: describe it as a one-time purchase; no subscriptions, no in-app
  purchases, no add-ons, no trials.

------------------------------------------------------------------
ProntoPC is a one-time paid app sold through the Microsoft Store for $9.99. There is no subscription,
no in-app purchase, no trial, no external checkout, no account, and no sign-in. Microsoft Store
commerce handles the purchase, and the Store price tier is the price (policy 10.8.4). The AI that
builds the repair plan is bundled inside the app and runs entirely on the device, so the app needs no
internet connection and no server to function.

REVIEWER ACCESS (policy 10.3.1): no code, account, sign-in, or test credentials are required. Acquire
ProntoPC through the Store (or install the submitted package) and launch it — all functionality is
immediately available, and it works with the machine offline.

HOW TO TEST:
1. Launch ProntoPC. There is no UAC prompt on launch and no login.
2. Click Scan. "Reading your system" runs for about a minute, entirely on-device and read-only.
3. The bundled on-device AI presents "Here's what we found" with recommended repairs, plus a live
   on-device Technician you can ask about the findings.
4. Optionally ask the Technician (it explains findings only and cannot perform repairs) or uncheck any
   item, then click Perform Repairs. Windows asks for administrator approval at this repair step only;
   approve it and ProntoPC runs the selected repairs.
5. When finished, the app shows "Your PC has been serviced." A full HTML report is saved to the
   Desktop and can be opened from the app; you can then close ProntoPC.

No network is used at any point — the entire test can be run with the machine disconnected after install.

PRICING (policy 10.8.4): one-time purchase, $9.99, via the Microsoft Store. No subscription, no in-app
purchases, no add-ons, no trial, no randomized purchases. Unlimited runs are included.

NOTES:
- There is no external payment, no promotion code, and no test account — the earlier submissions used a
  third-party checkout that could not be reached in the certification environment; that has been removed
  entirely. The app is now a self-contained paid Store app.
- Administrator permission is deferred until the reviewer chooses Perform Repairs; launch, scan, AI plan
  presentation, and the report do not require elevation.
- The repair plan is generated on-device by a bundled model (int4 google/gemma-3-1b-it via ONNX Runtime
  GenAI), so it works with no network connection.
- The completion report is generated on-device and saved locally to the Desktop; nothing is uploaded.
- On-screen figure: the app shows an animated Custodian character that reflects the current step
  (reading, planning, repairing, done). It is a visual status indicator only — it performs no action on
  its own.
- Support: support@prontopc.online
------------------------------------------------------------------

## What changed since the last submission (for our records)

Prior rejection (2026-09-17 certification report on v1.2.7.0):
- **10.3.1 App Is Testable / Test Account** — the reviewer could not reach the product; the app stopped
  at "Could not reach our server," and the provided test account did not work.
- **10.8.4 Financial Transactions / Purchase Types and Prices** — purchase types/price info was missing
  or inaccurate.

Root cause: both findings were artifacts of the **external, server-gated pay-per-run model** — a
third-party Stripe checkout plus a Cloud Run backend that the app had to reach before it would do
anything. In the certification environment that gate could not be reached, so the reviewer never got to
the product, and the external purchase drove the pricing scrutiny.

Fix (the pivot): ProntoPC is now a **one-time paid Microsoft Store app** with **no external payment, no
account, and no server dependency**. The primary functionality — the on-device AI technician — was
already local; removing the payment gate makes the app self-contained and testable:
- No test account, no promotion code, no sign-in — the Store entitlement is the gate.
- No server round-trip anywhere on the critical path; the app runs fully offline.
- 10.8.4 is satisfied by the Store price tier ($9.99, one-time); there is no in-app or external purchase
  to describe.

Earlier fixes that remain in force:
- Mandatory self-elevation was removed from startup; the app opens without a UAC prompt, and admin is
  requested only at Perform Repairs.
- The repair plan is authored on-device with no cloud fallback; if the on-device author cannot produce a
  plan, the app shows honest recourse and never substitutes a rule-based plan under the AI claim.

## UI & AI-governance posture in this build (for our records)
- The findings/repair flow is fronted by the Custodian actuator: an on-stage vector figure that moves
  through the real pipeline states (greeting, scanning, thinking, presenting, repairing, complete) driven
  only by true events — never a timer — and marks each real repair action as it completes. It is
  observation-only: it shows the work, it does not generate user-facing language.
- The conversational AI Technician runs entirely on-device: every inbound turn is PIM-screened, every
  outbound reply passes the integrity/RBC/PRISM boundary, and complete turns are retained in a
  hash-chained interaction ledger. It is grounded in the session findings and explains them; it never
  executes or authorizes a repair. Perform Repairs remains the separate explicit-consent path.

## Pre-submission checklist (ours, not for the reviewer)
- [ ] The MSIX bundles the Canon-formed on-device model at Assets/models/<formed model>/
      (genai_config.json + weights + formation manifest).
- [ ] Partner Center **Pricing**: one-time base price **$9.99** (or nearest available tier) is set.
- [ ] Partner Center **Product declarations**: no in-app purchases; no third-party/external commerce.
- [ ] Store **listing metadata** describes a one-time purchase with no subscriptions/IAP/add-ons/trials.
- [ ] The submitted app contains **no** Stripe/checkout/token/`prontopc://`/Cloud-Run session code on the
      critical path (payment gate removed; completion report generated locally).
- [ ] The completion screen offers **Open Report** and **Close ProntoPC**, and the report is written
      locally to the Desktop.
- [ ] `python certification_preflight.py` reports CERT_READY.
- [ ] WACK passes on the exact submitted package version; set the version in the notes block above.
