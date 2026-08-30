# Totem Lanes (the shard model)

One append-only file **per writer**, so concurrent agents never contend on the same
id or the same bytes. This is the fix for the failure mode that a single shared
`totems.jsonl` has under parallel work: two agents reaching for the same "next number,"
and two processes appending to one file at the same instant and interleaving mid-line.

## How it works

- Each writer owns exactly one lane file here: `totems.<lane>.jsonl`
  (e.g. `totems.claude.jsonl`, `totems.codex.jsonl`). **You only ever write your own.**
- Lane totem ids are `TOTEM-<LANE>-<NNNN>` — e.g. `TOTEM-C-0040`. The number is that
  lane's own counter; **never compare numbers across lanes.**
- The canonical ledger is the **merge** of the legacy trunk (`../totems.jsonl`) and every
  lane, ordered by `utc`. `verify_totems.py` does this merge at read time; there is no
  shared file to write, so there is nothing to race.
- The "head" (most recent Totem) is simply the latest entry by `utc` across all lanes.

## Writing an entry (the whole protocol)

1. Pick your lane letter/name (Claude = `claude`/`C`, Codex = `codex`/`X`). Keep it stable.
2. Append one compact JSON line to `totems.<lane>.jsonl` (LEDGER_STANDARD Rule 2), with
   `totem_id` `TOTEM-<LANE>-<NNNN>` (your last lane number + 1) and a `lane` field.
3. Run `python pod/totems/verify_totems.py`. It merges trunk + all lanes and confirms.

That's it — no reading another agent's tail, no id reservation, no lock. You read only
your own lane's last line to get your next number.

## Compatibility

- The legacy trunk `../totems.jsonl` stays valid and is still read + verified. Entries
  already there (TOTEM-0001 … the frozen shared history) are not moved. New knowledge
  goes to a lane, not the trunk.
- With no lanes present, the verifier behaves exactly as the single-file verifier did.

*Append-only, one writer per file. Order the world by `utc`, not by number.*
