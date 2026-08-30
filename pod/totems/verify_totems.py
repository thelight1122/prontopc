"""
Totem verifier — the mechanism that makes a Totem a TRUE sign.

Re-hashes every file each Totem claims and compares to the recorded SHA-256.
A cold-start instance runs this FIRST: matched Totems still hold; a mismatch
means the code moved since that Totem was written, so its picture is stale and
calls for a fresh audit before it is relied on. Drift is made visible here rather than inherited.

Usage:  python pod/totems/verify_totems.py
Exit 0 = every Totem holds. Exit 1 = at least one Totem has drifted (or a file
it references is now missing). Exit 2 = no Totem exists and a baseline audit is required.
"""
import hashlib
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]          # H:\PRONTOPC_PRODUCTION
LEDGER = Path(__file__).resolve().parent / "totems.jsonl"


def sha256_upper(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def main() -> int:
    if not LEDGER.exists():
        print("BOOTSTRAP REQUIRED — no totems.jsonl exists.")
        print("Perform a read-only repository audit, append the first hash-bound Totem, then rerun this verifier.")
        return 2

    totems = [json.loads(l) for l in LEDGER.read_text(encoding="utf-8").splitlines() if l.strip()]
    if not totems:
        print("BOOTSTRAP REQUIRED — totems.jsonl contains no Totem entries.")
        print("Perform a read-only repository audit, append the first hash-bound Totem, then rerun this verifier.")
        return 2
    print("=" * 70)
    print("ProntoPC — TOTEM VERIFICATION  (%d totem(s))" % len(totems))
    print("=" * 70)

    # Latest-wins: the current claimed state of each file is its NEWEST Totem.
    # Append-only means a file legitimately evolves across Totems; only the most
    # recent reference is authoritative, so intentional evolution is not drift.
    current = {}  # path -> (totem_id, recorded_sha)
    for t in totems:
        for rec in t.get("files", []):
            current[rec["path"]] = (t["totem_id"], rec["sha256"])

    drift = False
    for path in sorted(current):
        tid, recorded = current[path]
        p = REPO / path
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
