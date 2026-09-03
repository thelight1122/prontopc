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
