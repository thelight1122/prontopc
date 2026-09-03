# POD Architecture — the Model as orchestrating center, Orbs & Satellites as its abilities

*Design capture (TOTEM-C-0113). No code change; this records a shared understanding on the
append-only record so it does not live only in a conversation thread. Boundaries unchanged:
gate `enabled:false`, observe-never-judge, educate-not-train, append-only.*

## 1. The Model is the orchestrating center of the POD

In the POD, the Educated Model is not a tool that a separate Orchestrator calls — it **is** the
orchestrating intelligence at the center. It takes the Prism-screened input, decides what the
situation needs, reads the Orbs' records for grounding, and coordinates the response. The
standalone deterministic `Orchestrator.cs` is scaffolding from the pre-POD design (where the
model was merely one "plan author"); in the POD its *coordinating* role belongs to the model, and
its *safety* responsibilities remain as the execution boundary (see §4).

## 2. The Orbs & Satellites give the Model its Abilities and Functions

The Model at the center is the intelligence; the **Orbs and Satellites are what give it its
abilities and functions** — its capabilities and its senses. They are how the one model reaches
beyond its own weights.

- **Computer Technician Orb** — repair knowledge (the Tech Library / Repair documentation) + the
  repair engine + the Action Envelope. It has **no client channel**; it scans, authors a plan
  against the audit, and POSTS its findings. It gives the Model the ability to *diagnose and act
  on a PC*.
- **Communications & Governance Orb** — the **Prism on a Portal** (PIM -> IRG -> RBC -> IDS,
  bidirectional). It is the front door: all customer conversation enters here. It gives the Model
  the ability to *communicate under governance*.
- **Satellites** are sub-capabilities under an Orb (e.g. the bundled local-model Satellite of the
  repair-plan Orb).

This generalizes cleanly: **if the Model were multimodal, it would gain an Audio Orb and a Video
Orb** — the same pattern, new senses. An ability the Model does not yet have is simply an Orb it
does not yet carry. Abilities are added by attaching Orbs, not by changing the Model's core.

## 3. Roles run at their proper tier: inference only where a genuine choice exists

The Model integrates many roles, but not every role costs a forward pass. A role is
**deterministic** precisely when its answer is *fully determined by its inputs* — there is no
decision to make, so running it as fixed code is not a downgrade, it is correct (inference over a
settled answer only adds variance and risk).

| Tier | Where a genuine choice exists | Examples |
|------|-------------------------------|----------|
| **Inference (the Model's compute)** | yes | authoring the repair plan; the Custodian conversation; the IRG/Prism resonance *observation* |
| **Deterministic rails (no decision, minimal compute)** | no | the 25-point inspection; SystemAudit completeness; Action-Envelope admissibility; the append-only hash-chain / relay; the consent gate |

The single dividing line — **genuine choice vs. fixed-by-inputs** — buys three things at once:

- **Efficiency** — on an int4/1B CPU model, compute is scarce; spend forward passes only where
  judgment lives.
- **Safety / certifiability** — the safety-critical validations are deterministic, so they are
  *provable* by the evals and cannot be "gotten wrong" by a stochastic read. This is why the app
  certifies. (See `inspection-is-deterministic-not-model`: the Model never decides/generates/fails
  the inspection.)
- **Coherence** — the Model can be the orchestrating center *without ever touching the settled
  things*, so "the Model is the Orchestrator" and "the rails are deterministic" are the same
  design, not a tension.

## 4. Everything the Model is grounded on arrives as an auditable Totem (one discipline)

The append-only invariant is the single discipline the whole POD obeys. Nothing the Model reasons
on arrives by an untracked side-channel: every input to the reasoning is a hash-bound, inspectable
record.

- **Umbilicals are the handshakes** — an Orb attaches to the Model (attach-don't-affect).
- **The inter-Orb seam is the append-only relay** — the Technician Orb PUBLISHES a record; the
  Comms/Governance Orb READS it (the dotted line between the two Orbs' Totems). A record, not a
  channel — which is exactly why it works on a **single** model.
- Both halves of the Technician Orb's grounding travel this way: the **per-repair findings**
  (`AppendOnlyFindingsRelay`, wired C-0112) and, by the same discipline, the general **Repair
  documentation (Tech Library)** — the agreed next unit.

## 5. The flow (both directions cross the Prism)

**Inbound (customer -> answer):**

```
Customer input
  -> Communications & Governance Orb  (the Portal; the Technician Orb has no client channel)
       -> PIM   (screen the wording)
       -> IRG   (OBSERVE coherence/resonance)
       -> RBC   (reflective boundary)
  -> the Educated Model
       -> for a tech question, grounded by the Technician Orb:
            - the Repair documentation (Tech Library) — general
            - this session's findings (the relay record) — specific to THIS repair
```

Where a message does **not** resonate, it is **not denied** — it is an **IDS reroute**
(Identify / Define / Suggest -> an open question back to the customer): a formation focus, never
a verdict or a gate. Nothing is refused.

**Outbound (reply -> customer):** the Model's reply crosses the Prism too — the reply-tuned
outbound read (C-0104) observes the reply's OWN coherence/resonance — so the Prism governs *both*
directions on the Comms/Governance side.

## 6. What stays deterministic (the execution boundary)

`Orchestrator.cs` does not disappear — its safety responsibilities are the rails the Model
orchestrates *within*: the SystemAudit precondition (only a valid, completed scan reaches
anything), Action-Envelope admissibility (no uncatalogued/inadmissible action can relay or run on
*this* machine), and consent-before-repair. The Model directs the flow and decides what to say and
what to consult; **it never becomes the thing that self-authorizes an action to execute.** That is
the structural half of self-regulating-by-design: the intrinsic formed disposition orchestrates
and holds; the deterministic boundary is the non-force rail.

---

*Related: `docs/SELF_REGULATING_EMBEDDING_THESIS.md`; memories `self-regulating-by-design`,
`aegis-append-only-invariant`, `aegis-ids-non-force-reroute`, `inspection-is-deterministic-not-model`,
`prontopc-pod-architecture`.*
