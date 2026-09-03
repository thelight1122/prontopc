# Totem Communications Pipeline System

**Status:** Formal Project Standard  
**Version:** 1.0.0  
**Applies to:** ProntoPC development continuity, peer handoffs, implementation records, verification records, and cold-start recovery.

## 1. Purpose

The Totem Communications Pipeline is ProntoPC's append-only continuity layer. It gives a new coding instance, a peer, or an auditor an inspectable account of what was observed, changed, verified, proposed, acknowledged, and left open.

The pipeline preserves continuity without converting a prior message into authority. A Totem illuminates the state that its writer observed at a particular time. It does not execute work, grant consent, open a gate, alter a Canon, or replace live verification.

## 2. Foundational Invariants

The pipeline will preserve these invariants.

1. **Append-only history.** A record will be added, never rewritten. New information will supersede an earlier statement through a later record rather than remove it.
2. **Hash-bound referents.** A record that names a created or changed file will bind that file's SHA-256. The record remains true only while the present bytes hold its binding.
3. **Independent verification.** A later reader will run the verifier before relying on a Totem's code-state claim.
4. **Writer ownership.** Each writer will append only to its own lane. No peer will edit, reorder, or relabel another writer's historical entries.
5. **Time ordering.** The merged ledger will be ordered by `utc`, not by identifiers from separate lanes.
6. **Explicit boundaries.** A communication record will distinguish verified evidence, an observation, a proposal, an acknowledgement, and completion evidence.
7. **Non-force.** A Totem will offer an inspectable continuation point. It will not compel a peer, user, model, or runtime component to act.

## 3. Scope and Authority Boundaries

### 3.1 Development continuity layer

Totems, SEED records, Tumbler records, source bindings, evaluation reports, and handoff records belong to the development continuity layer. Their purpose is illumination of the build and its evidence trail.

### 3.2 Runtime continuity layer

Runtime records, including `AppendOnlyInteractionLedger` and `AppendOnlyFindingsRelay`, belong to the running Product. They retain auditable interaction and inter-Orb evidence. They do not derive runtime authority from the development Totem ledger.

The two layers share the append-only principle while remaining separate components with separate data, failure domains, and authority boundaries.

### 3.3 Consent and execution

A Totem will not authorize a package release, deployment, payment action, repair action, gate activation, Canon alteration, or shared-infrastructure mutation. Those actions will retain their own explicit consent and implementation evidence.

## 4. Ledger Topology

The canonical ledger is a time-ordered merge of:

```text
pod/totems/totems.jsonl                 legacy trunk; historical read-only continuity
pod/totems/shards/totems.claude.jsonl   Claude writer lane
pod/totems/shards/totems.codex.jsonl    Codex writer lane
pod/totems/shards/totems.<writer>.jsonl future writer lane
```

The trunk remains valid history. New records will enter a writer lane. A lane identifier has local meaning only; `TOTEM-C-0110` and `TOTEM-X-0082` are not globally sequenced by number.

## 5. Record Contract

Every entry will be one UTF-8 JSON object on one line and will conform to `pod/contracts/totem.schema.json`.

Required record fields are:

| Field | Meaning |
| --- | --- |
| `schema_version` | Record-contract version. |
| `totem_id` | Lane-local immutable record identifier. |
| `utc` | UTC observation or completion time. |
| `summary` | Compact statement of the record's evidence and boundaries. |
| `files` | Zero or more hash-bound file referents. |
| `advances` | Affected Orbs, declared state advancement, and no implied authority. |
| `next_gated_step` | One clear continuation point. |
| `trust_state` | `draft`, `provisional`, or `verified`. |

When a file is bound, its referent will contain:

```json
{"path":"repository/relative/path","sha256":"UPPERCASE_SHA256","state":"created|modified|deleted"}
```

An empty `files` list is appropriate for a receipt, observation, proposal, or acknowledgement that makes no source-state claim.

## 6. Communication Classes

| Class | What it records | What it will not claim |
| --- | --- | --- |
| Observation | A live check, result, or state discovered by the writer. | That an untested change is complete. |
| Receipt | That a peer record was read and understood. | Agreement beyond the stated analysis. |
| Proposal | A bounded design or next step. | Authorization or completed implementation. |
| Handoff | A scoped request and the continuation point for a named peer. | That the recipient accepted the work. |
| Acknowledgement | The recipient's acceptance, refinement, or objection. | Completion before evidence exists. |
| Implementation | Source/configuration/artifact changes and their bindings. | Runtime success without fresh checks. |
| Verification | Reproducible command results and their scope. | Coverage outside the executed surface. |
| Reconciliation | A later binding or correction after detected drift. | Erasure of earlier historical records. |
| Completion | The bounded outcome, evidence, residual limits, and next gate. | Product, certification, or deployment readiness beyond stated evidence. |

## 7. Standard Peer Communication Flow

The standard Claude/Codex flow will be:

```text
1. Verify ledger
2. Read newest relevant Totem(s)
3. Inspect named source or artifact when the record indicates it
4. Assess: verified fact / proposal / drift / unresolved boundary
5. Append a writer-lane observation, receipt, or handoff
6. Recipient appends acknowledgement in its own lane
7. Recipient performs the accepted bounded work
8. Recipient appends implementation and verification evidence in its own lane
9. Any peer independently verifies the result and appends a receipt or audit
```

This is a communication and illumination loop, not an execution loop. A handoff stays a request until its recipient acknowledges it. An acknowledgement stays an intent until a later implementation and verification record binds actual evidence.

## 8. Cold-Start Protocol

Every new coding instance will establish repository state in this order:

1. Locate `pod/totems/totems.jsonl` and the shard lanes.
2. Run `python pod/totems/verify_totems.py`.
3. Read the newest relevant record only after seeing the verifier result.
4. Use `pod/CURRENT-STATE.md` as a derived navigation view, not independent authority.
5. When a named binding holds, inspect only the files and evidence needed for the next bounded step.
6. When drift appears, audit the named files before making a present-state claim. A successor reconciliation record will bind the actual state when the responsible writer has finished the change.

## 9. Verification Semantics

`python pod/totems/verify_totems.py` will merge the trunk and all writer lanes, validate record shape, and compare each active referent to present bytes.

| Result | Meaning | Appropriate response |
| --- | --- | --- |
| `HOLDS` | The current referent matches the latest active binding. | Use it as evidence within its recorded scope. |
| `RETIRED` | The record intentionally binds an archived or absent referent. | Treat it as historical, not active source. |
| `DRIFT` | Current bytes differ from the latest binding, or the referent is missing. | Audit the named surface; do not rely on the older claim. |

The verifier validates integrity of the recorded source picture. It does not prove behavioral correctness, test sufficiency, security, deployment state, certification status, or Canon disposition by itself. Those conditions will have their own fresh evidence.

## 10. Concurrency Standard

Concurrent writers will avoid shared-record contention through lanes.

- A writer will read only the tail of its own lane to select its next identifier.
- A writer will append only one compact JSON line to its own lane.
- A writer will never repair another writer's drift by editing their record.
- The responsible writer will append a successor record after completing and verifying its scoped change.
- A peer may record a receipt or independent audit in its own lane without claiming ownership of another peer's source change.

## 11. Handoff Standard

A handoff will identify:

1. the recipient or responsible role;
2. the bounded objective;
3. current verified evidence and exact artifact paths;
4. explicit exclusions and retained gates;
5. expected acknowledgement form;
6. completion evidence and verification commands; and
7. the next consent boundary, where applicable.

For example, a model-to-POD handoff will identify the formed adapter hash, the local CPU runtime boundary, the relevant Orbs, the shared governance pathway, append-only runtime records, and the fact that formation alone does not establish deployment or certification readiness.

## 12. Product Governance Relationship

For ProntoPC Product communications, both user-originated and model-originated content will travel through the same declared governance pathway:

```text
PIM -> IRG -> RBC -> PRISM State of Flow -> IDS when rerouting is indicated
```

Totems will record development evidence about that pathway. They will not stand in for the Product's runtime gates, SystemAudit binding, envelope validation, session boundary, consent handoff, or append-only runtime ledger.

## 13. Completion Standard

A bounded work item will be represented as complete only when its completion Totem includes:

- changed or created referents with current SHA-256 bindings;
- exact verification commands and results;
- the scope directly proven by those commands;
- residual limits or unproven conditions; and
- the next gated step, if any.

Words such as “complete,” “ready,” or “certifiable” will refer only to the evidenced scope in the record. A broader claim will wait for the evidence that demonstrates it.

## 14. Operational Checklist

Before appending:

- [ ] The ledger verifier has run.
- [ ] The writer has identified the proper lane and next lane-local ID.
- [ ] Referent paths are repository-relative and hashes are current.
- [ ] The summary distinguishes facts from proposals and pending work.
- [ ] No historical record is being edited.

After appending:

- [ ] The verifier has run again.
- [ ] The writer has reported whether the ledger holds or names drift.
- [ ] A peer handoff identifies the acknowledgement and completion evidence expected.

## 15. Standard Statement

The Totem Communications Pipeline is the formal append-only continuity system for ProntoPC development. It will carry illuminated evidence between instances and peers while preserving source authority, consent boundaries, local writer ownership, and the distinction between a record of work and the work itself.
