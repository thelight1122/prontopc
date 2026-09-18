# Proposal — Pivot ProntoPC to a One-Time Paid Store App

**Status:** DRAFT — seeking unanimous consent (Tracey · Claude · Codex)
**Date:** 2026-09-17
**Product:** ProntoPC · Product ID `9P1K9QJXKND8` · Publisher: 5D Service Solutions Global
**Supersedes:** TOTEM-0029 (Stripe pay-per-run, wired into the app) — revisits and re-affirms the
direction of TOTEM-0028 (paid Store app), now with 9 additional rejections of evidence.
**Append-only:** if consented, sealed by a new Totem that points back to 0029/0028; nothing is edited or deleted.

---

## 1. Why we are reconsidering — the evidence

This is the **10th certification rejection**. The most recent (v1.2.7.0, review completed **09/17/2026**,
status *Attention needed*) returned two findings:

- **10.3.1 App Is Testable · Test Account** — *"…primary functionality requires a test account, but the
  test account that was provided doesn't work. Please resubmit and provide test account information…"*
  The report's own screenshot shows the app on the reviewer's Surface Laptop stopped at a dark error wall:
  **"Could not reach our server. Please try again or contact support@prontopc.online."**
- **10.8.4 Financial Transactions · Purchase Types and Prices** — *"Information about the types of items
  offered for in-product purchase and the range of prices is missing or inaccurate."*

## 2. Root-cause analysis

**Both findings exist only because of the external, server-gated pay-per-run model.**

The current architecture charges ~$29 **per run** through **external Stripe checkout**, and the app
**hard-gates on a Cloud Run backend** to verify a paid session token before it will do anything. In the
certification environment that gate failed to reach the server — so the reviewer never reached the
product at all ("could not reach our server"), which reads to Microsoft as *"not testable / test account
doesn't work"* (10.3.1). The external-purchase model is simultaneously what triggers the purchase-types/
pricing scrutiny (10.8.4).

**The irony:** the primary functionality — the embedded AI technician — is **already fully on-device**
(Gemma 3 1B int4 authors the repair plan locally; ModelSmoke proves `provenance=local-model`, offline).
The backend exists **only to collect the per-run payment**. The thing that keeps failing cert is the one
thing the actual product does not need.

## 3. The proposal

Convert ProntoPC to a **one-time paid Microsoft Store app**:

- **One-time purchase** via the Store's own commerce (Microsoft handles the transaction).
- **No external payment, no account, no login, no server round-trip** on the critical path.
- **Unlimited runs** — install once, run whenever the customer wants.
- **Periodic updates** through the normal Store update mechanism to keep it current.
- **Website becomes an information site** for the app (small changes): describe the product, then a
  **"Get it on Microsoft Store"** button. Remove the checkout/success/payment flow.
- The **embedded AI technician and the self-governing POC are unchanged** — they are on-device and are
  the product's core value; this pivot only removes the payment plumbing in front of them.

## 4. How it resolves the rejection history

| Rejection finding | Under a one-time paid app |
|---|---|
| **10.3.1** not testable — "test account doesn't work," server unreachable | **Removed.** No account and no server round-trip. The Store entitlement *is* the gate; Microsoft reviewers test paid apps natively. There is nothing left that can be "untestable." |
| **10.8.4** purchase types/prices missing/inaccurate | **Removed.** No external Stripe, no in-app purchase to describe. The Store **price tier is the price**, declared in Partner Center. |

These two findings are *artifacts of the external gate*. Removing the gate removes the findings — and the
whole recurring failure class behind 10 rejections.

## 5. Pricing

**Proposed one-time price: $9.98** (a `.98`/`.99` psychological price point).

**Rationale (Tracey):** volume over margin — *"I would rather get 1,000 purchases at $9.98 than 20 at $29."*
A low, frictionless one-time price removes the repeated-payment resistance of the per-run model, converts
far better, and reads as a fair "buy-once, use-forever" utility.

> **For Codex to verify:** confirm **$9.98 is an available Microsoft Store price tier** (Store tiers are a
> fixed set; the nearest standard tier may be **$9.99**). Confirm an existing product can be moved to a
> paid base price in Partner Center, and whether that has any bearing on the existing listing/identity.

## 6. Scope of changes

**App (`StoreApp/`)**
- Remove: the GATE panel, the Stripe payment link, `prontopc://run` token verification, `await-payment`
  polling, and all Cloud Run session calls (`submit-diagnostics`, `session/report`, `repair-start`, etc.).
- Keep: the full real product — scan → on-device AI plan → findings → consent → repair → completion.
- Change: the completion **report is currently fetched from the backend and saved to Desktop** → generate
  it **locally on-device** from the actual results. No server anywhere.
- **Do NOT re-introduce a network gate.** In particular, if any Store-license/entitlement check is added,
  it must **fail open offline** — installation implies purchase (see §7, open question 1).

**Website (root `*.html`)**
- Strip the checkout/success flow (`success-store.html`, payment gate pages) → clean info site + a
  "Get it on Microsoft Store" button. (Also shrinks the cloned-legacy web layer that has been a recurring
  drift source.)

**Backend (Cloud Run `pronto-server-cloudrun.mjs`)**
- Retired from the certification path. Archive per the never-delete rule; keep only if wanted for the
  marketing site's contact form or future telemetry — never on the app's critical path.

**Store listing (Partner Center)**
- Set a one-time **paid base price** ($9.98 / nearest tier).
- Notes for Certification stated plainly: **no login, no account, works fully offline** — install, launch,
  click Scan; the AI plan runs on-device.

## 7. Open questions — for Codex to scrutinize (where we most want a second set of eyes)

1. **Runtime entitlement check.** For "unlimited runs," do we add a Store license check
   (`StoreContext.GetAppLicenseAsync`) or treat *installed = purchased* and add no check at all? A runtime
   license query can need connectivity — **this is the exact trap that caused the rejection**. Recommended:
   no runtime check, or if present, **fail open when offline**. Does Codex agree, or see a licensing
   requirement we must honor?
2. **Price tier reality.** Is `$9.98` an actual selectable tier, or is it `$9.99`? Any constraint on
   converting this specific product to paid?
3. **Free → paid conversion.** Can Product ID `9P1K9QJXKND8` be moved to a paid base price without a new
   identity, and does that reset anything (ratings, prior submission lineage)?
4. **System-modifying repairs.** The app changes system state during repair (with consent). Any Store
   policy that a *paid, unlimited* PC-repair utility must additionally disclose or gate? (Restore points,
   reversibility statements, etc.)
5. **Report generation.** Any objection to generating the completion report fully locally, and where it
   should be written (Desktop vs. Documents vs. in-app view + optional save)?
6. **Existing Stripe artifacts.** Any live per-run customers or Stripe/Firestore state that needs a
   migration or teardown plan, versus simply being archived?
7. **Anything we missed.** The point of unanimous consent — Codex, name any risk, policy, or simpler path
   we have not considered.

## 8. Trade-offs (stated honestly)

- **Revenue model change:** per-run metering → one-time purchase. We lose per-use revenue but gain
  conversion, simplicity, and — the entire point — a product that can actually pass certification. If
  recurring revenue is ever wanted, the Store supports **subscriptions** natively (still server-free on our
  side); one-time is the cert-cleanest starting point.
- **No make-good coupon needed:** the AI is on-device and always available, so the "AI unavailable"
  make-good path is removed rather than migrated.
- **Piracy/sharing:** a paid Store app can be shared like any other; we accept this rather than reintroduce
  a server gate that risks cert.

## 9. What stays exactly the same

- The **embedded AI technician** and the **self-governing model POC** (Canon SEED, Tech Library, the
  bidirectional non-force Prism: PIM → IRG → RBC → Prism, IDS reroute) — all on-device, unaffected, and
  in fact cleaner without the payment plumbing around them.
- The **repair flow, consent model, and Action Envelope** safety boundaries.
- The **on-device Gemma** and its formation track.

## 10. Consent

Unanimous consent to proceed (each party records agreement, questions, or a counter):

- **Tracey:** ☐ approve · ☐ changes requested
- **Claude:** ☐ approve · ☐ changes requested — *(authored this draft; recommends approval — it removes the
  root cause of the rejection history, not just this instance)*
- **Codex:** ☐ approve · ☐ changes requested — *(review requested per §7; add anything missed)*

On unanimous consent, seal with a Totem superseding TOTEM-0029, then execute §6 in order:
app payment-gate removal + local report → website info-site → listing price + cert notes → resubmit.
