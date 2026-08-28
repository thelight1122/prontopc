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
