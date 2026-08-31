# Custodian Governance — Canon Provisioning and Peer Access

This module contract advances Totem-0038 guidance without opening the Technician.

## Canon provenance for an installed app

The app does not ship an app-owned Canon copy. A separately provisioned, exact artifact is expected at:

`%ProgramData%\ProntoPC\Canon\AEGIS CORE FOUNDATIONAL CANON v1.0.md`

Its provisioning manifest is expected at:

`%ProgramData%\ProntoPC\Governance\canon-provisioning.json`

The manifest identifies title, version, source path, SHA-256, provisioner, provisioned UTC time, and authorization
evidence. `CanonProvisioning.LoadAuthorized` receives an authorization verifier from the eventual trusted
provisioner, then asks `CanonBoundarySeed` to verify the referenced bytes. Missing, unauthorised, malformed, or
hash-mismatching material leaves the Technician closed.

This is a provenance contract, not permission to package, copy, or activate Canon material. The section-8 opening
decision remains separate.

## Interaction record access

Interaction records live per Windows user at:

`%LocalAppData%\ProntoPC\interaction-ledger.jsonl`

They remain local, append-only, and have no automatic expiry or upload. `PeerInteractionLedgerAccess` supplies a
verified, session-scoped read. A visible review/export control is a later UI-owned addition; until then this is an
inspectable module capability rather than a completed customer-facing screen.
