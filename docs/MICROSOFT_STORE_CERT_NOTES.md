# ProntoPC - Microsoft Store Certification Notes

Product: ProntoPC
Product ID: 9P1K9QJXKND8

Paste the block below into Partner Center -> Submission -> "Notes for certification"
when resubmitting. Fill nothing else in - ProntoPC needs no test account (access
is granted by the checkout session, there is no login).

Before submitting, confirm in the Stripe Dashboard (LIVE mode) that the promotion
code MSCERT2026 still exists and is 100% off.

------------------------------------------------------------------
ProntoPC is an AI-powered Windows diagnostic and repair service. The core feature
is a one-time $29 diagnostic + repair run. No account or sign-in is required -
access is granted by the checkout session. To test the full paid experience at no
cost, use the 100%-off promotion code below. No payment card is required.

HOW TO TEST (full functionality, free):
1. Launch ProntoPC. It opens the diagnostic experience at https://prontopc.online.
2. Click "Start Your Diagnostic" (or "Get Started"). This opens the Stripe
   checkout page.
3. On the checkout page, click "Add promotion code" and enter:
       MSCERT2026
   The total changes from $29.00 to $0.00 and NO payment card is required.
4. Enter any valid email address, then complete the (free) checkout.
5. You are returned to https://prontopc.online/success and your Custodian session
   opens.
6. From the success page, download the Pronto Agent and run it on the Windows PC
   you want checked. The agent scans the system, sends the diagnostics to our AI
   engine, shows the proposed repair plan, and (after you confirm) runs safe,
   reversible repairs, saving an audit report to the Desktop.

NOTES:
- No real payment is taken anywhere in this flow with the code above.
- No account or sign-in is required; the session is created from the checkout.
- A confirmation email with the agent download link is also sent to the address
  entered at checkout.
- Support: support@prontopc.online
------------------------------------------------------------------

## What changed since the last submission (for our records)
Prior rejection (10.1.2.10 Functionality): after completing the $0 checkout, the
redirect to /success failed with "Could not reach our server."

Root causes fixed:
1. Payment verification (verifyStripeSession in pronto-server-cloudrun.mjs) only
   accepted payment_status "paid". The 100%-off tester checkout settles as
   "no_payment_required"/status "complete", so it threw and /api/verify-payment
   returned 402. It now accepts paid, no_payment_required, or a complete session.
   Deployed to Cloud Run (us-central1), revision pronto-pc-00070+.
2. The AI engine (Azure OpenAI) billing tier was restored so repair-plan
   generation succeeds.
