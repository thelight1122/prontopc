# ProntoPC — AEGIS Self-Governance Method: Certification-Facing Disclosure

**Product:** ProntoPC · **Product ID:** 9P1K9QJXKND8 · **Publisher:** 5D Service Solutions Global

*Purpose of this document.* A plain, accurate statement of the AEGIS governance the ProntoPC
product actually uses, written for certification review. It is deliberately bounded to **verified
Product behavior in the shipping certification candidate** — it distinguishes active behavior from
historical or development-only components and makes no claim beyond what the product does. This
document changes no product behavior.

---

## 1. What ProntoPC uses AEGIS for

ProntoPC applies **AEGIS as Product-level architecture** in three areas:

1. **On-device model formation and grounding** — the bundled responder is formed (educated) from
   the AEGIS Canon and curated technical repair knowledge. The bounded Product Nature Layer is
   packaged as runtime evidence-domain context; it supplies no facts or execution authority.
2. **Deterministic evidence controls** — the diagnostic, the audit, the admissible-action
   catalogue, explicit consent, and append-only evidence, none of which are decided by the model.
3. **A bidirectional communication-governance pipeline** (PIM → IRG → RBC → PRISM → IDS) —
   active for the post-scan Technician conversation described in §4.

These are **auditable Product controls** supporting coherence, transparency, continuity, and
non-force. They are **not** a claim of infallibility, jailbreak-proofing, hack-proofing, autonomous
execution, or guaranteed correctness (see §6).

---

## 2. What is ACTIVE and evidenced in this certification candidate

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
- **Governed post-scan conversation.** After validated findings are presented, the customer may ask
  the on-device Technician about those findings before choosing **Start Repair** or **Skip Repair**.
  Each inbound turn is PIM-screened before it reaches the model; each generated reply is passed
  through the integrity/RBC/PRISM boundary before display and is written with its inbound context
  to the hash-chained interaction ledger.

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

## 4. Boundary B — the communication-governance pipeline is ACTIVE in this release

The PIM → IRG → RBC → PRISM → IDS pipeline described in §3 is active on the Findings screen. The
post-scan Technician is a genuine on-device model conversation, not canned web copy and not a
cloud conversation:

- **Inbound:** every customer message is screened by PIM before model invocation. The current
  Product behavior observes and retains the message context rather than silently discarding it.
- **Grounding:** the Technician is seeded from the session-bound, append-only findings relay. It is
  directed to explain those established findings and not invent additional diagnostic facts.
- **Outbound:** each model reply traverses the integrity/RBC/PRISM boundary before it is shown, and
  the completed turn is recorded atomically in the hash-chained interaction ledger. IDS supplies
  the Product's reroute form where clarification or re-grounding is needed; it is not a punitive
  denial mechanism.
- **Scope:** the bundle contains the Canon-formed plan-authoring model. The experimental analyzer
  is not shipped as a separate runtime model. The Canon is the model's embedded formed disposition,
  not a runtime Canon file.

The conversation explains the already-established findings and repair choices. It does **not**
execute, authorize, or silently add a repair. **Start Repair** remains the only path to the
separate explicit-consent and deterministic envelope checks of §2.

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
- guaranteed factual accuracy in every conversational detail. The model is grounded by the
  session findings relay and formed toward non-force expression, while the PRISM boundary addresses
  interaction form and tone rather than serving as a guarantee of diagnostic truth.

---

## 7. Provenance and auditability

- Development provenance is kept as an **append-only, hash-bound Totem ledger** (supersede, never
  overwrite); runtime evidence is kept as **append-only, hash-chained records**. Both are reversible
  and inspectable.
- This disclosure is bound in the Claude-lane Totem that records it, with the document's content
  hash, answering the Codex request (X-0084) to Totem any certification-document change.

*Scope note: this document is a statement of method and Product behavior for certification review. It
introduces no code change and enables no gate.*
