"""
Totem verifier — the mechanism that makes a Totem a TRUE sign.

Re-hashes every file each Totem claims and compares to the recorded SHA-256.
A cold-start instance runs this FIRST: matched Totems still hold; a mismatch
means the code moved since that Totem was written, so its picture is stale and
calls for a fresh audit before it is relied on. Drift is made visible here rather than inherited.

Ledger sources (all read; the LEDGER_STANDARD Rule 1 parse-per-line applies to each):
  • pod/totems/totems.jsonl              — the legacy shared trunk (frozen history).
  • pod/totems/shards/totems.<lane>.jsonl — one append-only lane PER writer (e.g. an SI
    agent). Each lane has exactly ONE writer, so concurrent agents never contend on the
    same file or the same id. Lane totem ids are TOTEM-<LANE>-<NNNN> (e.g. TOTEM-C-0040).

The canonical ledger is the MERGE of the trunk and every lane, ordered by `utc`
(trunk entries win ties, then lane order). Latest-wins-per-file then runs over that
one merged, time-ordered sequence — so a file's authority is simply whoever bound it
most recently in real time, regardless of which lane wrote it. Shards are additive:
with no shards present, this behaves exactly as the single-file verifier did.

Usage:  python pod/totems/verify_totems.py
Exit 0 = every Totem holds. Exit 1 = at least one Totem has drifted (or a file
it references is now missing). Exit 2 = no Totem exists and a baseline audit is required.
"""
import hashlib
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]          # H:\PRONTOPC_PRODUCTION
HERE = Path(__file__).resolve().parent
LEDGER = HERE / "totems.jsonl"                       # legacy shared trunk
SHARDS = HERE / "shards"                             # per-lane append-only files


def sha256_upper(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_jsonl(path: Path):
    """Parse each non-empty line as one JSON object (LEDGER_STANDARD Rule 1)."""
    if not path.exists():
        return []
    return [json.loads(l) for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]


def load_ledger():
    """Return (ordered_totems, lane_counts). Order = merge of trunk + lanes by utc,
    trunk winning ties (so legacy same-timestamp file order is preserved), then lane
    order. Trunk keeps its file order; lanes are pre-ordered by (utc, totem_id)."""
    trunk = load_jsonl(LEDGER)
    lane_counts = {}
    lane_entries = []
    if SHARDS.is_dir():
        for shard in sorted(SHARDS.glob("totems.*.jsonl")):
            lane = shard.stem.split(".", 1)[1] if "." in shard.stem else shard.stem
            entries = load_jsonl(shard)
            if entries:
                lane_counts[lane] = len(entries)
                lane_entries.extend(entries)
    lane_entries.sort(key=lambda e: (e.get("utc", ""), e.get("totem_id", "")))

    # source rank 0 = trunk (wins utc ties, keeps file order), 1 = lanes.
    tagged = [(e.get("utc", ""), 0, i, e) for i, e in enumerate(trunk)]
    tagged += [(e.get("utc", ""), 1, i, e) for i, e in enumerate(lane_entries)]
    tagged.sort(key=lambda t: (t[0], t[1], t[2]))
    return [t[3] for t in tagged], lane_counts


def main() -> int:
    totems, lane_counts = load_ledger()
    if not totems:
        print("BOOTSTRAP REQUIRED — no Totem entries in the trunk or any lane.")
        print("Perform a read-only repository audit, append the first hash-bound Totem, then rerun this verifier.")
        return 2

    lanes_note = ""
    if lane_counts:
        lanes_note = "  [lanes: %s]" % ", ".join("%s×%d" % (k, v) for k, v in sorted(lane_counts.items()))
    print("=" * 70)
    print("ProntoPC — TOTEM VERIFICATION  (%d totem(s))%s" % (len(totems), lanes_note))
    print("=" * 70)

    # Latest-wins over the merged, time-ordered sequence: the current claimed state of
    # each file is its NEWEST Totem (by utc across trunk + all lanes). Append-only means
    # a file legitimately evolves, so only the most recent reference is authoritative.
    current = {}  # path -> (totem_id, recorded_sha, state)
    for t in totems:
        for rec in t.get("files", []):
            current[rec["path"]] = (t["totem_id"], rec["sha256"], rec.get("state"))

    drift = False
    for path in sorted(current):
        tid, recorded, state = current[path]
        p = REPO / path
        if state == "deleted":
            # The newest Totem declares this path RETIRED — moved to archive/ under the
            # never-delete house rule, so history stays reversible. It HOLDS while the path
            # is genuinely absent; a reappearance is undeclared drift (record it in a Totem).
            if p.exists():
                print("  [UNDEAD ]  %s  (authority %s) — recorded deleted but present" % (path, tid))
                drift = True
            else:
                print("  [RETIRED] %s  (%s) — verified absent (archived)" % (path, tid))
            continue
        if not p.exists():
            print("  [MISSING] %s  (authority %s)" % (path, tid))
            drift = True
            continue
        actual = sha256_upper(p)
        if actual != recorded:
            print("  [DRIFT ]  %s  (authority %s)" % (path, tid))
            print("            recorded %s… actual %s…" % (recorded[:16], actual[:16]))
            drift = True
        else:
            print("  [HOLDS ]  %s  (%s)" % (path, tid))

    print("=" * 70)
    if drift:
        print("DRIFT DETECTED — at least one Totem no longer matches the tree.")
        print("Re-audit the affected files before trusting the Totem's picture.")
        return 1
    print("All Totems hold. The recorded code-state picture is still true.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
