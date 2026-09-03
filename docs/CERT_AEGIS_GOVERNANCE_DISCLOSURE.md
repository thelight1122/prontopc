# ProntoPC — AEGIS Self-Governance Method: Certification-Facing Disclosure

**Product:** ProntoPC · **Product ID:** 9P1K9QJXKND8 · **Publisher:** 5D Service Solutions Global

*Purpose of this document.* A plain, accurate statement of the AEGIS governance the ProntoPC
product actually uses, written for certification review. It is deliberately bounded to **verified
Product behavior in the shipped build** — it distinguishes what is *active* in this release from
what is *implemented architecture but not active*, and it makes no claim beyond what the product
does. (Origin: proposed by the Codex lane in TOTEM-X-0084; reconciled and drafted here per
TOTEM-C-0114 / C-0115. This document changes no product behavior.)

---

## 1. What ProntoPC uses AEGIS for

ProntoPC applies **AEGIS as Product-level architecture** in three areas:

1. **On-device model formation** — the bundled AI is *formed* (educated) with the AEGIS Canon and
   Nature Layer plus curated technical repair knowledge.
2. **Deterministic evidence controls** — the diagnostic, the audit, the admissible-action
   catalogue, explicit consent, and append-only evidence, none of which are decided by the model.
3. **A bidirectional communication-governance pipeline** (PIM → IRG → RBC → PRISM → IDS) —
   implemented as Product architecture (see §4 for its status in this release).

These are **auditable Product controls** supporting coherence, transparency, continuity, and
non-force. They are **not** a claim of infallibility, jailbreak-proofing, hack-proofing, autonomous
execution, or guaranteed correctness (see §6).

---

## 2. What is ACTIVE and evidenced in this shipped build

The controls below are present and operative in the certified application:

- **On-device plan authoring.** The AI that builds the repair plan is a model bundled inside the
  app and runs on the device; the plan is generated locally with no dependency on any internet
  service, and there is **no cloud AI fallback**.
- **A read-only diagnostic (deterministic).** A fixed 25-section system inspection is performed by
  the application (not the model). It changes nothing on the PC. The model never decides, generates,
  or fails the inspection.
- **System-Audit precondition (deterministic).** The read-only diagnostics are validated into an
  immutable, evidence-hashed audit; **only a complete, valid audit may reach the model, findings,
  consent, or repair.** An incomplete or synthetic diagnostic set is declined before any model call.
- **Admissible-action envelope (deterministic).** Every action in an authored plan is validated
  against a versioned catalogue of admissible actions for *this* machine; an uncatalogued or
  inadmissible action is dropped and can neither be presented nor executed.
- **Explicit consent before repair.** No repair runs without the customer's explicit per-item
  approval; the app executes only the server-authorized action set the customer acknowledged.
- **Append-only evidence.** Interaction and findings records are written as hash-chained,
  append-only entries (tamper-evident, inspectable), never overwritten.

**Repair actions are therefore tied to validated system evidence, the admissible action envelope,
explicit user consent, and append-only evidence** — in every shipped run.

### Boundary A — technical repair knowledge is *formed into the model*, not a runtime library

The product's technical repair knowledge is **formed into the model** (educated into the weights),
together with the Canon and Nature Layer. ProntoPC does **not** load, read, or depend on a separate
runtime reference-document library for this knowledge at run time. Any description of the repair
knowledge should say it is *formed into the model*, not that the app consults a "runtime reference
library."

---

## 3. The PRISM logic (what each stage does)

The AEGIS communication-governance pipeline is **observe-and-reroute, never deny**:

- **PIM** — identifies evident incoherence in a message (a screen, not a judge).
- **IRG** (Integrity Resonance Gate) — **observes** coherence/resonance across the AEGIS virtues.
  It illuminates what *is*; it does **not** judge, score, rank, pass, or fail a person.
- **RBC** — supplies boundary and context (reflective boundary conditions).
- **PRISM — State of Flow** — permits evidence-and-context resonance to continue toward the model.
- **IDS** (Identify · Define · Suggest) — where content does **not** resonate, it is **not denied**;
  it is **rerouted to its originator** for clarification, grounding, or repair, as an open question.
  Nothing is refused; the way forward is a formation focus, never a verdict or a gate.

This pathway is **bidirectional**: it applies to inbound (customer → model) and outbound
(model reply → customer) alike.

---

## 4. Boundary B — the communication-governance pipeline is implemented architecture, DISABLED in this release

The PIM → IRG → RBC → PRISM → IDS pipeline described in §3 is implemented as Product architecture,
but **the peer-facing conversational path is disabled in this release**:

- The governance seam ships **closed** (the peer-facing conversation gate is set to *disabled*), so
  the pipeline does **not** actively govern any shipped user communication — because **no
  conversational feature is active in this build**. The shipped, active AI behavior is the on-device
  **repair-plan authoring** described in §2, whose output reaches the customer as deterministic,
  templated findings, not as free-form conversation.
- The on-device analyzer that would instrument the IRG read is a **development component** and is
  **not bundled** in the certified package (this package bundles only the plan-authoring model).
- The append-only findings record exists and is written, but it is read only by that (currently
  disabled) conversational path.

Accordingly, a certification statement should describe the communication-governance pipeline as
**implemented Product architecture**, and say plainly that **in this release the conversational path
is disabled**, so the shipped active controls are the **on-device formation of §2 plus the
deterministic evidence / envelope / consent / append-only rails.** Enabling the conversational path
is a deliberate, separate, future act — not part of this submission.

---

## 5. No autonomous execution

The model **directs reasoning and grounding; it never self-authorizes an action to execute.** Every
repair is gated by the deterministic rails of §2 — validated audit, admissible-action envelope, and
explicit user consent. There is no path by which a model output alone causes a change to the PC.

---

## 6. Explicit non-claims

These are auditable Product controls supporting **coherence, transparency, continuity, and
non-force.** They are **not**, and this disclosure does not assert:

- infallibility or guaranteed correctness of any diagnosis, plan, or repair;
- jailbreak-proofing or hack-proofing of the model or the application;
- autonomous execution (see §5 — every repair is user-consented and envelope-bound);
- that the communication-governance pipeline is active in this release (see §4 — it is not).

---

## 7. Provenance and auditability

- Development provenance is kept as an **append-only, hash-bound Totem ledger** (supersede, never
  overwrite); runtime evidence is kept as **append-only, hash-chained records**. Both are reversible
  and inspectable.
- This disclosure is bound in the Claude-lane Totem that records it, with the document's content
  hash, answering the Codex request (X-0084) to Totem any certification-document change.

*Scope note: this document is a statement of method and Product behavior for certification review. It
introduces no code change and enables no gate.*
