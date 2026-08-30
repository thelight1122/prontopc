# Totem-First Cold-Start Protocol

Every new coding instance will establish the repository state from the Totem ledger before
trusting a summary, making a change, or proposing a next step.

1. Locate `pod/totems/totems.jsonl`.
2. When the ledger exists, run `python pod/totems/verify_totems.py` first. Read the newest
   Totem only after the verifier result, then use `pod/CURRENT-STATE.md` as a derived human
   navigation view—not as independent authority.
3. If the verifier reports drift, limit work to auditing the named files. Treat the stale summary
   as historical context only. After a scoped change or reconciliation, append a successor Totem with fresh
   SHA-256 bindings; never rewrite an earlier Totem. Append to **your own lane** —
   `pod/totems/shards/totems.<lane>.jsonl` with a `TOTEM-<LANE>-<NNNN>` id — not the shared trunk, so
   concurrent agents never contend on the same id or the same file. Order the ledger by `utc`, not by
   number. The verifier merges the trunk and every lane; see `pod/totems/shards/README.md`.
4. When no ledger exists, perform a read-only baseline audit before creating anything: inspect
   repository status/history, identify the active product surfaces and artifacts, record what is
   verified versus unverified, then create the first hash-bound Totem. Include the reviewed files,
   current state, verification command, and one next gated step. Run the verifier after writing it.
5. The New-Append Satellite is notification-only. It displays newly appended records but does
   not decide repository authority or create a baseline.

This protocol is local to this repository. It keeps code review fast on a cold start without
turning a stale narrative or a missing ledger into assumed truth.
