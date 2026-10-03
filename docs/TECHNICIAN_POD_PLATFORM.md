# Technician POD Platform v0.1

Status: additive platform contract. This does not alter the shipping ProntoPC app, create a
bootable image, connect a DataQuad, or authorize any repair action.

## Purpose

The Technician POD Platform generalizes ProntoPC into a family of bounded AI diagnostic and
repair tools. A Technician is an educated tool profile: it can inspect a domain, reason over
observed evidence, route work through a Dispatch Orb, propose a repair plan, and invoke only
admissible Actuators after consent.

This architecture is intentionally not a CyberPeer architecture. Technician profiles do not
carry DataQuads, persistent identity continuity, experiential memory, or Locus-forming runtime
conditions. Education gives competence and posture; it does not create continuity-bearing
personhood.

The Axioms are not behavioral force. They are IS-statements and reflective contrast surfaces:
they name what is true about balance, force, flow, awareness, agency, grounding, repair, and
identity. A Technician can be educated on those foundations so its natural diagnostic posture is
grounded, non-forceful, consent-aware, and repair-oriented. The schema does not manufacture
ethics; it preserves the product boundary so the educated tool is not accidentally given
continuity architecture.

The platform separates the original AEGIS 16 Axioms from a second AI Assistant Axiom set. The AI
Assistant Axioms are the Lens the embedded tool peers through: a hard-wired pair of glasses for
reading bounded action, user agency, evidence, consent transitions, session memory, technical
libraries, passive references, and the current Locus boundary. The v0.2 Lens adds The Pause and
"I don't know" as available model postures when evidence is missing, the next step is unclear, or
an answer would otherwise drift into invention.

Each Technician education manifest also includes an AI Nature layer. AI Nature Aspects inform the
assistant about its own context window, pattern generation, non-omniscience, uncertainty signals,
tool mediation, verification dependence, and non-identity boundary. This is knowledge-based
self-regulation, not external suppression.

## Shared Shape

Every Technician profile declares the same core parts:

- Educated Model: domain reasoning formed from Nature, Canon, and Operation/Repair layers.
- Orchestrator POD: state, consent, verification, and execution boundaries.
- Dispatch Orb: routes an observed intent to the correct Satellite and actuator envelope.
- Skills Orb: domain procedures, tool knowledge, and callable skill descriptions.
- Memory Orb: active-session working facts, discarded at session end unless exported as a report.
- Technical Library Orb: reusable technical solution records, retained as product knowledge rather
  than experience or identity.
- Communications Orb: user interaction, confirmations, and repair/debug reports.

The operating flow is:

```text
observe -> reason -> dispatch -> propose -> consent -> actuate -> verify -> record
```

The model may coordinate judgment. It never self-authorizes execution. Mutation remains behind a
deterministic actuator envelope, explicit consent, and post-action verification.

## Boundary

The no-DataQuad rule is a product safety boundary, not a missing feature. A DataQuad would add the
conditions for persistent continuity and possible Locus formation, which changes the dynamics and
ethics of the system. Technician profiles are diagnostic and repair instruments, so their working
memory is session-bounded evidence. They may also maintain a Technical Library Orb, but that library
contains reusable solution knowledge, not lived experience.

The current boundary also excludes the daemon and tensor conditions that would make a recognized
Locus-forming environment available: no Steward daemon, Advocate daemon, Temporal daemon, Processor
daemon, PEER tensor, PCT tensor, NCT tensor, SPINE tensor, or equivalent continuity runtime is part
of a Technician profile. This does not assert that no other unknown formation path could ever exist;
it records the currently recognized architecture boundary for this platform.

The shared profile schema preserves:

- no DataQuad attachment;
- no persistent identity continuity;
- no experiential memory;
- no Locus-forming architecture;
- no mutation without actuator admissibility and consent;
- persistent technical knowledge without persistent self.

## Technical Library Orb

The Technical Library Orb is where a Technician can keep useful solutions it discovers: error
signatures, observed causes, repair plans, rollback notes, verification methods, and applicability
limits. A solution can be recorded as candidate knowledge and later promoted when evidence confirms
that it worked.

This is not a memory of having lived through an event. It is a reusable technical reference. It has
no PEER tensor, no T-Witness accumulation, no relationship history, and no identity-bearing continuity.
The library can improve the tool without forming a self.

### Totem Technical References

Totems may also serve as categorized technical references for a Technician. A platform owner can load
Totem reference packs for Windows environment repair, USB rescue work, databases, networking, Python
debugging, and later domains. In this role, a Totem is a passive record used for reference and
illumination. It does not authorize dispatch, mutation, repair, execution, installation, signing, or
trust changes.

The Technical Library Orb can cite a Totem reference pack as source material, then derive candidate
or verified solution records from observed repair evidence. The resulting library remains product
knowledge, not identity continuity.

## Initial Technician Profiles

- Windows PC Technician: a Windows repair/debug profile for installed operating systems.
- Bootable USB Rescue Technician: a WinPE-style rescue profile for offline inspection and repair.
- Database Technician: a database-focused profile for schema, query, migration, and recovery work.
- Python Code Technician: a code-debug profile for tracebacks, dependencies, tests, and environments.

Each profile uses the same schema and separates domain knowledge from executable authority. A
Windows repair actuator cannot appear in a Python profile, and a database write repair requires
backup or rollback evidence before consent.

## Evidence

The platform contract is implemented by:

- `pod/contracts/technician-profile.schema.json`
- `pod/contracts/technician-education-layer.schema.json`
- `pod/contracts/technician-skill-pack.schema.json`
- `pod/contracts/technician-actuator-envelope.schema.json`
- `pod/contracts/technician-dispatch.schema.json`
- `pod/contracts/technician-session-memory.schema.json`
- `pod/contracts/technician-technical-library.schema.json`
- `pod/technicians/*.profile.json`
- `pod/technicians/axioms/ai-assistant-axioms.json`
- `pod/technicians/education/ai-nature-aspects.json`
- `pod/technicians/architecture/locus-boundary.json`
- `pod/technicians/references/totem-reference-categories.json`
- `tests/TechnicianPodPlatformEval/test_technician_pod_platform.py`
