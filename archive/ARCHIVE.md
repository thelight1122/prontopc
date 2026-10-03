# ProntoPC — Design Creation Arc (archive)

These files are **superseded intermediates**, kept for a reversible, auditable trail of how the
current ProntoPC design system came to be. They are not live — nothing in the app links to them —
but each was a real step in the reasoning that produced the current "ISness." Archived, not deleted.

Archived: 2026-08-28. Live design system: Pronto purple/iris (see `memory/prontopc-aegis-redesign.md`).

---

## The arc, in order

1. **`landing-hero.html`** — the first fully *coded* Pronto hero.
   - Origin: a design critique of a mockup image the user shared. The user's key insight:
     the cartoon mascot + thumbs-up exists to **reduce tension** for a stressed user with a broken PC.
   - Established the mascot's voice rule that still governs all Pronto copy:
     **"confident invitation, never a directive."** ("Relax" / "Breathe" were rejected as commands;
     "Let's get your computer running smoothly again — pronto!" won because "Let's" is a *confident invitation*.)
   - Established the purple/iris tokens, the `.btn-primary`/`.glass`/`.tile` components, the rotating
     rings + halo hero composition, and the thumbs-up mascot (`pronto-thumbsup.png`).
   - Superseded by: `index.html` (the coded sections were rebuilt there under Lumin's baked hero).

2. **`repair-complete.html`** — standalone "Repair Complete" page.
   - Origin: the user loved the hero and asked to reuse it as the post-repair completion screen.
     "YOUR PC. RESTORED. PRONTO." reads as a *finished-repair statement*, not just a pitch.
   - Introduced the completion pattern: mint "Repair Complete" badge, "What Pronto handled" results
     card, report/print buttons, and Pronto's completion line **"All done — your PC's running like new."**
   - Superseded by: the `#phase-complete` screen inside `success.html`, which carries this design but
     is wired to live session data (MB freed, action list, coupon) and stays cohesive with the flow.

3. **`landing.html`** — Lumin's baked-image hero, button-wired.
   - Origin: the user had "Lumin" produce an HTML overlay of the original landing (a single 1792×1024
     rendered PNG with invisible percentage-positioned hotspot `<a>` regions + a separate coded mobile view).
   - This file = that original (`~/Downloads/ProntoPC-Landing-Page.html`) with every hotspot wired to the
     real Stripe checkout / index sections, plus four added nav hotspots (How It Works / Services /
     Security / About) and the Pronto favicon.
   - Superseded by: `index.html`, which embeds this exact hero and adds the coded content sections below it.
   - Note: the original source still exists at `C:\Users\TraceyPrutch\Downloads\ProntoPC-Landing-Page.html`.

---

## Why keep them
The live pages show *what* the design is. These show *why* — the decisions, the rejected options, and
the mascot-voice rule that a future instance would otherwise have to re-derive. They are the thought
process behind the current state.

---

## Retired source modules

- **`StoreApp-Governance/CanonProvisioning.cs`** — retired 2026-09-01.
  - Was the installed-app resolver for an **external** AEGIS Canon file provisioned to
    `%ProgramData%\ProntoPC\Canon\…` via an authorized, hash-verified manifest.
  - Retired because the architecture was corrected (Tracey): **the Canon is not external to the
    model** — it is the model's embedded foundation, *what the model stands on*, not a file to load,
    hash, inject, or provision. "Everything needs to be locally available to the model." With the
    Canon in-model, external provisioning has no role.
  - Replaced by the in-model `CanonBoundarySeed` (no filesystem; a reflective-posture seed only) and
    a single-switch `PipelineGovernance.OfferingAllowed` (the §8 `enabled` gate; no external-Canon
    check). Kept here (not deleted) for the reversible trail. See the Totem recording this rework.

- **`StoreApp-Governance/TotemReferenceLibrary.cs`** — retired 2026-09-03 (same session it was started).
  - Was to be the versioned, hash-bound **runtime "Totem Reference Library"** (TOTEM-X-0084 vocabulary):
    load the curated Windows repair reference (`pod/orbs/custodian-stage/knowledge/windows-repair-reference.md`),
    verify it against a pinned (version, sha256), and read it into the Comms Orb's grounding on the same
    append-only discipline as the findings relay.
  - Retired because the repair knowledge is **already FORMED into the model** via education (baked into the
    weights). Tracey: "if it's already baked into the model we can leave it there if it isn't hurting anything
    — it should help if nothing else." A runtime reference would duplicate the weights, and claiming a
    separate "runtime Totem Reference Library" in the cert disclosure would OVERCLAIM (the honesty boundary
    raised in TOTEM-C-0114). So the disclosure stays truthful: the repair knowledge is **formed**, not a
    runtime reference library.
  - Never landed (never committed, never Totem-bound, MainWindow was not wired). Kept here for the reversible
    trail — if a runtime auditability/versioning layer is ever wanted, recover from here.

---

## Retired build artifacts (large, untracked — marked in place, not moved)

- **`release-candidates/ProntoPC_1.2.0.0-x64-a60e978-storeupload/ProntoPC.Package_1.2.0.0_x64.msixupload.retired`**
  (TOTEM-C-0163, 2026-09-04) — the stale **v1.2.0.0** Store `.msixupload`. Renamed `.retired` (not moved into
  `archive/`, which is git-tracked — a 679 MB binary would bloat the repo) so the certification preflight's
  package auto-pick no longer selects it. Superseded by the fresh **unsigned v1.2.0.6** submission artifact
  (`StoreApp/ProntoPC.msix`, TOTEM-C-0162; a copy at `release-candidates/ProntoPC_1.2.0.6_x64_unsigned_partnercenter.msix`).
  Reversible — rename back to `.msixupload` to restore it. It predates the on-device formed model, the §8
  Technician, and every UI/reliability change since 1.2.0.0, so it must not be submitted.

---

## Retired web payment flow (one-time paid Store app pivot, TOTEM-C-0174, 2026-09-17)

Moved to `archive/web-payment-flow/` when the website became an **information site** for a one-time paid
Microsoft Store app (proposal `docs/PROPOSAL-ONE-TIME-PAID-APP.md`; consent C-0171 / X-0116; Tracey "go ahead
with the split"). Every page here depended on the Stripe checkout + Cloud Run backend that the pivot removes:

- **success.html**, **success-store.html** — post-payment verify + `prontopc://run` scan hand-off.
- **test-pay.html** — $1.00 live-Stripe test checkout.
- **guardian-offer.html**, **guardian.html** — Stripe subscription (Guardian) sign-up + activation.

The served site is now **index + terms + privacy** only, with a "Get it on Microsoft Store" CTA and no payment
or server dependency (`vercel.json` routes for the retired pages removed). Reversible — recover from here if a
Store-native subscription (Guardian) is ever wanted.

---

## Retired Cloud Run backend source (one-time paid Store app pivot, TOTEM-X-0118, 2026-09-18)

Moved to `archive/cloudrun-backend/` when the installed StoreApp stopped using the Stripe/Cloud Run
pay-per-run lifecycle. This preserves the exact former backend source and deployment notes without leaving
them at the repository root as an active service bundle:

- **`pronto-server-cloudrun.mjs`** — former Cloud Run HTTP service for Stripe checkout verification,
  paid-session token minting, session/plan/consent/repair report recording, refund helper paths, and
  AI-unavailable make-good coupons.
- **`package.json`** — former Node/Firestore Cloud Run package manifest.
- **`.gcloudignore`** — former Cloud Run source upload filter.
- **`deploy/`** — former Cloud Run env/deploy/runbook notes.
- **`ProntoAgent/`** — former Python desktop/CLI agent source that sent diagnostics to the Cloud Run
  repair-plan API.

This archive move is source-level only. It does not delete the live Google Cloud Run service, Stripe objects,
Firestore data, Secret Manager values, DNS, deployment history, or customer evidence. Any live teardown remains
a separate owner-approved gate.

## cert-notes-1.2.21/ (2026-10-03)
- `CERT_NOTES_SHORT.txt`: the reviewer notes written for v1.2.21.0 (AI disclosure footer, AEGIS link, content reporting). Archived when the active package was rolled back to v1.2.20.0 (TOTEM-X-0176), which has none of those features. Restore it if the 1.2.21 line returns.
