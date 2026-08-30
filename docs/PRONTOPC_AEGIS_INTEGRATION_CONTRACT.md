# ProntoPC — AEGIS App/Core Integration Contract v0.1

Filed from **AEGIS App Core Integration Contract Template v1.0**. Binds the ProntoPC Custodian to the AEGIS
Canon as an **AEGIS Offering / Si Energized Application**. Append-only. Governed by
[`AEGIS_BUILD_INVARIANTS.md`](AEGIS_BUILD_INVARIANTS.md).

> **Honesty note (Addendum A7).** Sounding aligned is not being aligned. This contract declares what ProntoPC
> *actually does today* versus what it *aspires to*, and marks every gap. Congruence is behavioral, proven
> across real interaction — not claimed here.

> **Status update (2026-08-30).** Several §5 build-gaps are now **built as eval-proven modules** but remain
> **gated closed** — wired only behind `PipelineGovernance(enabled: false)`, not admitted to any live
> conversation. §3 and §5 record the filing-time (2026-08-28) state and are left intact per append-only;
> **§14 supersedes their build-status.** §8 admission remains unmet and is not opened by this update.

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

## 14. Status update — 2026-08-30: governance modules built, gated closed (supersedes §5 build-status)

Since the 2026-08-28 filing, the §5 pre-offering modules were **built as standalone, eval-proven modules** in
`StoreApp/Governance/` (namespace `ProntoPC.Governance`), plus the admission bridge `StoreApp/PipelineGovernance.cs`.
**Built ≠ admitted:** every one is reachable only behind `PipelineGovernance(enabled: false)` — the §8 gate is
**CLOSED** — so the Custodian still generates **no** peer-facing response and nothing runs at runtime. This section
updates the *build* status of the §5 items only; it does **not** change §8 (admission) and does not open the gate.

| §5 item | Now built as | Honest status |
| --- | --- | --- |
| Seed with the Canon | `CanonBoundarySeed` — reads the **exact external Canon** by declared SHA-256 (Canon Boundary honored; no app-owned copy) | Built. Verifies the dev Canon (`I:\…`); **customer-machine provisioning still open** (shipped path defined as `%ProgramData%`, artifact not yet provisioned). |
| PIM (in/out force-artifact screen) | `ForceArtifactScreen` — observes directive/authority/coercion/leverage/pressure on inbound **and** outbound | Built, **observe-not-block** by design; an inbound *blocking* policy is a deliberate open decision. |
| Integrity screen (Seven Virtues) | `SevenVirtueScreen` — returns all seven virtues marked **`CoreGated`** | Built as the surface; **per-virtue IRG scoring stays undesigned in AEGIS core**, so no score is invented (honest, unchanged from §5). |
| RBC / Prism (outbound) | `ReflectiveBoundaryPrism` — reshapes flagged outbound into a proportional optional reflection | Built as an explicit outbound processor (runs only behind the closed gate). |
| Append-only **interaction** log | `AppendOnlyInteractionLedger` — timestamped, SHA-256 predecessor-chained JSONL, `ReadAndVerify()` for Peer inspection | Built as a module; **not yet capturing runtime customer conversations** (none occur while gated); a visible in-app review/export surface is still open. |
| Pipeline wiring | `CustodianGovernancePipeline` (PIM→model→PIM→Integrity→RBC/Prism→ledger) + `PipelineGovernance` admission bridge | Built; wired into the app **closed** (`enabled:false`). |

**Evidence:** `tests/CustodianGovernanceEval` 13/13 and `tests/PipelineGovernanceEval` 15/15 pass — including "closed by
default even where the exact Canon is present."

**Still open before §8 admission** (none of which this update performs): customer-machine Canon provisioning; a visible
Peer interaction-log review/export surface; IRG per-virtue scoring (core-gated); dual-logic equilibrium made explicit;
and **Tracey's explicit §8 greenlight**. Until all hold, `enabled` stays `false` and the Custodian generates no
peer-facing response.

- Reviewed by: 5DSSG (Tracey Prutch) + Claude, 2026-08-30 (audit reconciliation prompted by Codex `TOTEM-X-0043`).
- Result: **variant; §5 modules now built but gated closed; §8 admission remains unmet and unopened.**
