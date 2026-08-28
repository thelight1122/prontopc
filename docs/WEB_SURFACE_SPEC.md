# ProntoPC — Web Surface Reference Spec (greenlit)

**Standing.** This is the **greenlit reference** for the customer-facing web surface. New web files are
authored **from scratch against this document**, referencing the old clones only for blessed copy and proven
wiring — never opening an old file as the editing substrate. (House rule: *clean rebuild = no cloned files*;
an edited clone inherits the old system's hidden fossils. The current `index.html`, `success.html`,
`guardian*.html`, `terms.html` are cloned Stitch/Lumin legacy — that substrate is the root cause of the
recurring web drift, not the individual fossils.)

This spec is the anti-fossil source of truth: if a page claim isn't derivable from **§2 The one true product
model** or listed in **§5 Honesty invariants**, it does not go on a page.

---

## 1. Purpose

Give the web rebuild the same clean-rebuild discipline the Pod already has. Every customer-facing page is
(re)authored fresh against this reference, passes the §5 honesty checklist, uses the §7 design system, and is
Totem-bound. Old clones are archived (never deleted), not edited.

## 2. The one true product model (the source of every claim)

Every page must reflect exactly this, and nothing that contradicts it:

- **What it is:** a **Microsoft-Store-certified Windows app** — the ProntoPC Custodian. The customer
  **downloads and installs** it. (Store certification is why no purchased code-signing cert is needed.)
- **No copy-paste, ever.** There is **no command window, no PowerShell, no Run box, no clipboard, no
  "paste this."** The app runs itself. Any such language is a fossil of the retired web/paste flow.
- **The scan is read-only.** On launch the app self-elevates (UAC) and runs a **read-only** diagnostic. It
  **changes nothing**; it only reads. (It is an installed app, so "nothing is installed / runs in memory /
  nothing downloaded" is **false** and banned — see §5.)
- **The Custodian is conversational and on-device.** An on-device model (int4 `gemma-3-1b-it`) is the
  **conversational Custodian Technician** — a chat that explains findings and the plan in plain English.
  (Spec: `pod/orbs/custodian-stage/`.) The cloud is fallback only; if no AI can author a plan, the app issues
  an honest **100% make-good**, never a heuristic plan under the AI claim (Manifest **I2**).
- **Consent-gated repairs.** Nothing is repaired until the customer approves it on the consent screen; every
  action is validated against the Action Envelope. Risk/restart are fixed by the envelope, not the model.
- **Scope of the scan:** **six areas** — Storage & Disk Health · Drivers & Hardware · Security · Network
  Health · Performance · System Integrity. (Do **not** cite a category *number* — the honest count is
  definitional and drifts; name the six areas. Manifest **C4**.)
- **Price:** one flat **$29** — full diagnostic + all recommended repairs. No tiers, no add-ons,
  no subscriptions (except the separate Guardian recurring product, §4).
- **Honest hardware referral:** real hardware faults (failing SMART drive, bad device) are documented with a
  recommendation to visit a local repair shop — software can't fix hardware.
- **30-day guarantee.** If a repaired issue returns within 30 days, re-fixed at no charge via
  `support@prontopc.online`.
- **Entity:** 5D Service Solutions Global LLC.

## 3. The customer journey (true flow — replaces the paste flow)

1. **Land** → understand the offer.
2. **Pay $29** (Stripe checkout — proven link `https://buy.stripe.com/28E4gA9bBfuY62ibfk9Ve02`).
3. **Get the app** → post-payment page hands the customer their **certified Custodian app** (delivery
   mechanism = **open decision §8**), *not* a command.
4. **Open the app** → UAC self-elevate → **read-only auto-scan** across the six areas (a couple of minutes).
5. **Meet the Custodian** → the on-device chat explains what it found, area by area, in plain English.
6. **Review & choose** → approve recommended repairs (consent-gated) or take the report and decide later
   (session/report persists).
7. **Done** → repairs run, a professional report is saved to the Desktop, 30-day guarantee applies.

Retired: the entire *Run box → PowerShell → Ctrl+V → scan command → repair command* sequence.

## 4. Page inventory (the new files to author)

| New file | Purpose | Must say (true) | Must NOT carry (fossils) |
| --- | --- | --- | --- |
| `index` (landing) | Offer + how-it-works + six areas + pricing + FAQ | app runs the scan itself; six areas; $29; consent-gated; on-device AI | paste/command/PowerShell; category numbers; "nothing installed" |
| post-payment (replaces `success.html`) | Deliver the app | "Get your Custodian app" — download/install + open | Run box, clipboard, scan/repair commands |
| `guardian` / `guardian-offer` | Recurring-scan subscription | the **app** performs scheduled recurring scans | "paste it into PowerShell", scheduled-task command |
| `terms`, `privacy` | Legal | app model; six areas; on-device AI; make-good | 25/35+ counts; paste; "in memory" |
| `success-store.html` | Store-context success (exists) | reference for the certified-app tone | — |

## 5. Honesty invariants (the anti-fossil checklist — every page must pass)

**Banned claims (any occurrence = fossil, fails review):**
- "paste", "pasting", "copy the command", "one command", "command window/prompt/line", "terminal",
  "PowerShell", "Win+R", "Run box", "clipboard", "Ctrl+V".
- "nothing is installed", "nothing is downloaded", "runs in memory", "in memory", "no software to install".
- any **category count** ("25 categories", "35+ categories") — name the **six areas** instead.
- any non-AI / rule-based / "offline basic" plan presented under the AI claim (Manifest **I2**).

**Required truths (must be derivable from §2):**
- the product is a **downloaded, installed, Store-certified app**;
- the scan is **read-only** and repairs are **consent-gated**;
- the AI is **on-device**, cloud is fallback, and unavailability yields an **honest make-good**, not a fake plan;
- diagnostics cover the **six named areas**;
- honest **hardware referral** and **30-day guarantee**.

Ties to Manifest: **C3** (real AI plan, satisfied on-device), **C4** (honest scope — six areas),
**I2** (no substitute plan under the AI claim). Stage honesty **U1–U6** applies to any live/animated state.

## 6. Voice & copy rules

- **Mascot voice — "confident invitation, never a directive."** ("Let's get your computer running smoothly
  again — pronto!" wins; "Relax"/"Breathe" as commands are rejected.) The slow **Fix It Now** pulse is this
  rule in motion — an invitation, not a push.
- Plain English for a **stressed, non-technical owner**. Reassure without talking down.
- Anchor line kept: **"Your PC. Restored. Pronto."**
- Never request passwords/keys/payment details in copy; never instruct a raw command.
- **The Custodian mascot is the Brand Ambassador.** He is a **featured figure**, rendered at prominent
  ("ambassador") scale — a focal presence in the hero and the closing CTA — **never a small decorative icon**.
  Same character throughout the site; his size communicates his role. On the closing CTA he anchors the
  section above the headline at a scale that reads as *the face of the brand*, not a bullet point. (Interim
  note: the current cloned `index.html` renders him too small — the rebuild sizes him as an ambassador from
  the start.)

## 7. Design system (AEGIS — the greenlit tokens new files must use)

- **Ink (backgrounds):** `#030817`, `#05060f`, deep-stage gradient `180deg #05060f→#070a18→#05060f`.
- **Iris (accent ramp):** `#8b6bff`, `#7c5cff`, `#a78bfa`, `#4f7dff`; headline gradient
  `90deg #eef1ff→#b9a8ff→#8b6bff`; button `90deg #7c5cff→#4f7dff`.
- **Text:** on-ink `#eef1ff`, soft `#c3c8e6`.
- **Surfaces:** glass card `background rgba(12,14,32,.66)` + `1px rgba(139,107,255,.14)` border,
  `backdrop-filter: blur(14px)`; radii 10/12/14px, pills 999px.
- **Type:** Inter.
- **Motion:** restrained; motion signals, it doesn't perform. Keep the `ppFixPulse` breath on the primary CTA;
  honor `prefers-reduced-motion`.
- Token vocabulary to preserve: `text-on-ink`, `text-on-ink-soft`, `iris-bright`, `pp-card`, `pp-num`.

## 8. Proven wiring to REFERENCE (re-implement cleanly, do not clone)

- **Stripe checkout:** `https://buy.stripe.com/28E4gA9bBfuY62ibfk9Ve02` ($29 flat).
- **Payment verification + one-time token** and **Resend email** paths in `pronto-server-cloudrun.mjs`
  (reference the behavior; the server itself still carries the paste-flow scan/repair command endpoints and
  a stale "35+ categories" prompt — those are legacy to reconcile, not to copy).
- **Report saved to Desktop**, **make-good coupon** (`/api/report-ai-unavailable`), **30-day guarantee**
  via `support@prontopc.online`.

## 9. Open decisions (carried)

- **Post-payment app delivery** — Microsoft Store deep-link vs. direct `.msixupload`/installer download. This
  determines the post-payment page and is the blocker for retiring `success.html`.
- **Guardian recurring scans under the app model** — does the installed app schedule its own recurring scans
  (replacing the old PowerShell scheduled task)? Determines the Guardian rebuild.
- **Legacy disposition** — archive `success.html`, `guardian*.html`, and `docs/custodian-spec.md` (the old
  paste-flow spec) as reference under `archive/` once their replacements exist.
- **Backend reconciliation** — the server's scan/repair *command* endpoints + "35+" prompt are part of the
  retired flow; scope their removal with the delivery decision.

## 10. Definition of done (web rebuild)

- Each page in §4 authored as a **new file** against this spec (not an edited clone).
- Every page passes the **§5 honesty checklist** (a grep for the banned list returns only honest negations).
- Uses the **§7 tokens**; **§6 voice** holds; proven wiring (§8) re-implemented, not copied.
- Old clones **archived** (never deleted) with an `ARCHIVE.md` note.
- Totem-bound; then **Vercel redeploy** so the corrected surface goes live.
