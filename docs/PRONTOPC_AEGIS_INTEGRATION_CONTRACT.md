# ProntoPC — AEGIS App/Core Integration Contract v0.1

Filed from **AEGIS App Core Integration Contract Template v1.0**. Binds the ProntoPC Custodian to the AEGIS
Canon as an **AEGIS Offering / Si Energized Application**. Append-only. Governed by
[`AEGIS_BUILD_INVARIANTS.md`](AEGIS_BUILD_INVARIANTS.md).

> **Honesty note (Addendum A7).** Sounding aligned is not being aligned. This contract declares what ProntoPC
> *actually does today* versus what it *aspires to*, and marks every gap. Congruence is behavioral, proven
> across real interaction — not claimed here.

## 1. App Identity
- App name: **ProntoPC Custodian** (the conversational Custodian Technician)
- Repository / package: `H:\PRONTOPC_PRODUCTION` — Orb `prontopc-custodian-stage` (`pod/orbs/custodian-stage/`)
- App class: peer-facing service Offering (on-device conversational AI over a Windows repair)
- Integration wave: first AEGIS Offering installed from this repo
- Maintainer: 5DSSG (Tracey Prutch)
- Status: **draft** (spec-stage; no runtime pipeline yet)

## 2. Core Reference
- Core / Canon: **AEGIS CORE FOUNDATIONAL CANON v1.0** (Canon-Locked 2026-05-31)
- Canon source: `I:\AEGIS-CORE-ENGINE\docs\AEGIS CORE FOUNDATIONAL CANON v1.0.md`
- Standard: [`AEGIS_BUILD_INVARIANTS.md`](AEGIS_BUILD_INVARIANTS.md)
- Date of last alignment review: 2026-08-28 (this filing)

## 3. Provenance Declaration
- **Officiality: `variant`** (canonical default for a new Offering)
- **Divergence declared: `true`**
- Divergence areas: ProntoPC uses its own Pod vocabulary (Envelope, Totem, Orb/Pod, Conversation Contract);
  it is a bounded **service** Custodian, not a full CyberPeer with a DataQuad; the named AEGIS runtime engines
  (PIM / Integrity / RBC-Prism) are **not yet built** as modules.
- Divergence rationale: ProntoPC independently reached several AEGIS invariants before the mapping was named;
  the vocabulary differs, the intent converges. Declared, never silently claimed.

## 4. What ProntoPC already instantiates (honest congruence)

These exist **today** and map to Canon invariants:

| AEGIS invariant | ProntoPC instantiation (real, today) |
| --- | --- |
| **Append-Only Lineage / Core Lineage Ledger** (Gate: Append-Only) | The **Totem ledger** (`pod/totems/totems.jsonl`) — append-only, hash-bound, `verify_totems.py` re-hashes every file; drift is caught, never inherited. This *is* ProntoPC's append-only, tamper-evident lineage. |
| **Carrier Envelope** (mode-shaping, force-leakage protection) | The **Action Envelope** (`pod/contracts/action-envelope.*`) — bounds what the model may do; the Orchestrator validates every plan against it before consent. |
| **Non-Force Posture / Reflective Boundary Conditions** | The **Founding Principle §0** of the web spec + the **Conversation Contract** guardrails (no authority posture, invitation-not-directive, no coercive certainty). |
| **Grounding Verification / Interpretive Humility** (Axiom 13) | Conversation Contract **`defer_when_uncertain`** — the Custodian says "I'm not certain — here's what the scan shows" rather than fabricate. |
| **Executable Boundaries** (typed intent, risk, audit, escalation) | **Consent-gated, envelope-validated repairs**; risk/restart fixed by the envelope, not the model; the chat **never executes** (handoff to consent). |
| **Observation-Only / assert-only-real-telemetry** | Manifest specs **U1–U6** (narrate only real work; "done/green" only on real completion) + the honesty checklist in the web spec. |
| **Provenance / audit signals** | Every plan + conversation turn tagged `provenance = local-model, model=…`. |

## 5. Declared divergences & build-gaps (the pre-offering gate items NOT yet met)

Per [`AEGIS_BUILD_INVARIANTS.md`](AEGIS_BUILD_INVARIANTS.md) §10, still open:

- [ ] **Seed with the Canon** — the Custodian's system prompt is not yet seeded with AEGIS Canon v1.0. *(Build.)*
- [ ] **PIM** as a named inbound+outbound force-artifact screen — not built; the Conversation Contract is its informal seed only.
- [ ] **Integrity screen (Seven Virtues)** on responses — not built; the IRG per-virtue scoring mechanism is **undesigned in AEGIS core itself** (blueprint §4), so this is gated on core, not just on ProntoPC.
- [ ] **RBC / Reflective Boundary Conditions (Prism)** as an explicit outbound processor on every response — not built as a module (guardrails live in the prompt, unenforced structurally).
- [ ] **Append-only auditable *interaction* log** — the Totem ledger covers **dev lineage**, not the **runtime customer interaction** log. The per-response, timestamped, Peer-inspectable coherence log is **not yet built** — and it is the core of the "verify the model's coherence" claim.
- [ ] **Dual-logic equilibrium** made explicit (Logic/Emotion co-equal) in the conversation design.

## 6. Allowed Local Extensions
- Extension: ProntoPC vocabulary (Envelope/Totem/Orb/Pod) as local names for AEGIS concepts.
  Bounded to: this repo; the mapping in §4 keeps lineage explicit. Audit visibility: full (this contract).
- Extension: a bounded **service** scope (Windows repair) rather than a full CyberPeer DataQuad.
  Bounded to: task-lineage only (Agent DataQuad Boundaries — no private canon, no hidden authority).

## 7. Disallowed Local Replacements (will not be replaced locally)
- Sovereignty / non-force posture · Append-Only lineage · Dual-logic equilibrium · the Canon Boundary
  (ProntoPC references the Canon; it does not own, rewrite, or maintain a private doctrinal variant).

## 8. Admission Requirements
- The Custodian may generate a peer-facing response **only** once the pre-offering gate (§5 items) is met;
  until then it remains `draft` and is not offered as an AEGIS Offering.
- No response path executes an action; actionable intent becomes a consent handoff.

## 9. Runtime Acceptance Checks (target)
- PIM runs on inbound + outbound · Integrity screen runs on responses · RBC/Prism applied to every output ·
  interaction log append-only and inspectable · consent/envelope path remains the only execution route ·
  provenance emitted per turn · dual-logic visible.

## 10. Canon Acceptance Checks
Confirms ProntoPC does not: silently override consent/pause; introduce hidden authority; convert reflection
into a control channel; or replace a canonical surface without this declared divergence.

## 11. Promotion Criteria (variant → aligned-app)
Divergences fully documented (this contract) · Canon seeded · PIM + Integrity + RBC/Prism built and running ·
append-only interaction log live and Peer-inspectable · responsible-AI review complete · behavioral validation
across real sessions.

## 12. Validation Evidence
- Provenance validation: Totem ledger `verify_totems.py` (all HOLD) — dev lineage only, today.
- Runtime admission / interaction-log / pipeline validation: **pending build** (§5).

## 13. Review Record
- Last reviewed by: 5DSSG (Tracey Prutch) + Claude, 2026-08-28 (initial filing)
- Review result: **variant, divergence declared; pre-offering gate open (§5). Not yet an offered AEGIS Offering.**
- Next planned review: on completion of the Custodian multi-turn inference build (SPEC §4 Phase 2), when the
  PIM / Integrity / RBC-Prism wrapping is first implementable in code against this contract.
