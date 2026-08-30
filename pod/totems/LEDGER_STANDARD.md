# Totem Ledger — JSON Format Standard

The single global standard for reading and writing `pod/totems/totems.jsonl`.

It exists because the format was never specified, and the resulting inconsistency
caused a real failure: two sessions wrote entries with different whitespace
(`"totem_id": "TOTEM-0029"` vs `"totem_id":"TOTEM-0030"`), and a reader that
pattern-matched the raw bytes silently missed the compact entries — reporting the
ledger two Totems shorter than it was. This standard closes that gap.

## The format: JSON Lines (NDJSON)

`totems.jsonl` is a JSON Lines file. Each non-empty line is exactly one complete
JSON object conforming to `pod/contracts/totem.schema.json`, encoded UTF-8 (no BOM),
terminated by a single `\n`.

## Rule 1 — Reading: parse, never grep

Any tool that needs a field (`totem_id`, `files`, `utc`, …) MUST parse each line as
JSON (`json.loads`) and read the field from the object. **Never** pattern-match the
raw bytes. Whitespace, key order, and spacing are NOT semantic and differ across
historical entries, so a text match will silently miss valid records — the exact bug
above. This rule is absolute and applies to every consumer: the verifier, the
New-Append Beacon, and any new tooling.

## Rule 2 — Writing: one compact object per line

New appends are written in canonical compact form — no insignificant whitespace
between tokens, equivalent to:

```python
json.dumps(obj, separators=(",", ":"), ensure_ascii=False) + "\n"
```

Keys follow the order declared in `totem.schema.json`. Exactly one object per line;
never pretty-print across multiple lines.

## Rule 3 — Append-only

Never edit or reformat an existing line. Historical entries written before this
standard keep their original spacing and remain valid — Rule 1 guarantees they still
read correctly. New knowledge is a new line, never a rewrite of an old one.

## Rule 4 — Lanes: one append-only file per writer (the shard model)

A single shared `totems.jsonl` is a **single-writer** format. Under parallel work
(two SI agents at once) it has two race points: the monotonic id (both reach for the
same "next number") and the file itself (two appends interleave mid-line and corrupt
it). The fix is **lanes**: each writer appends only to its own file under
`pod/totems/shards/`, named `totems.<lane>.jsonl`.

- **IDs are lane-local:** `TOTEM-<LANE>-<NNNN>` (e.g. `TOTEM-C-0040`). Optionally carry a
  `lane` field. Never compare numbers across lanes — two lanes may both sit at `0040`.
- **One writer per file:** you only read your own lane's last line to get your next
  number; you never read or write another agent's lane. No id reservation, no lock.
- **The canonical ledger is a merge, not a file:** `verify_totems.py` reads the legacy
  trunk (`totems.jsonl`) plus every lane and orders the union by `utc` (trunk wins ties).
  The "head" is the latest entry by `utc` across all lanes. Order the world by time, not
  by number.
- **The trunk is frozen legacy:** `totems.jsonl` (TOTEM-0001 … the shared history) stays
  valid and is still read + verified; new knowledge goes to a lane. Rules 1–3 apply to
  each lane file exactly as to the trunk. With no lanes present, everything behaves as
  the single-file ledger did. See `pod/totems/shards/README.md`.

---

*Governs the ledger only. Standalone contract files (`*.schema.json`) and the state
doc (`CURRENT-STATE.md`) are ordinary formatted files, not JSONL, and are exempt.*
