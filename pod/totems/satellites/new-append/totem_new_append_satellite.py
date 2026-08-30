"""Read-only Totem New-Append Satellite.

It monitors only the append-only Totem JSONL ledger.  It never writes a cursor,
modifies the ledger, or imports application code: a new line is the sole signal.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
from pathlib import Path
from typing import Any


DEFAULT_LEDGER = Path(__file__).resolve().parents[2] / "totems.jsonl"


def sha256_prefix(path: Path, length: int) -> str:
    """Hash exactly the baseline bytes, so a rewrite cannot masquerade as append."""
    digest = hashlib.sha256()
    remaining = length
    with path.open("rb") as stream:
        while remaining:
            chunk = stream.read(min(65536, remaining))
            if not chunk:
                break
            digest.update(chunk)
            remaining -= len(chunk)
    return digest.hexdigest()


def format_new_append(entry: dict[str, Any]) -> str:
    """Return the single display line for one well-formed appended Totem."""
    return "NEW APPEND | {id} | {utc}".format(
        id=entry.get("totem_id", "UNKNOWN"), utc=entry.get("utc", "unknown time")
    )


class TotemAppendMonitor:
    """Tracks a byte offset in one ledger process without persisting any state."""

    def __init__(self, ledger: Path) -> None:
        self.ledger = ledger
        self.offset = 0
        self._baseline_digest = ""

    def baseline(self) -> None:
        """Ignore entries that existed when watch mode began."""
        self.offset = self.ledger.stat().st_size
        self._baseline_digest = sha256_prefix(self.ledger, self.offset)

    def read_appends(self) -> tuple[bool, list[dict[str, Any]]]:
        """Return (ledger_reset, complete JSON entries appended after the offset)."""
        size = self.ledger.stat().st_size
        if size < self.offset:
            self.offset = size
            self._baseline_digest = sha256_prefix(self.ledger, self.offset)
            return True, []
        if self._baseline_digest != sha256_prefix(self.ledger, self.offset):
            # The bytes we had already accepted changed. Do not announce anything
            # beyond that point as a new append; establish a new read-only baseline.
            self.offset = size
            self._baseline_digest = sha256_prefix(self.ledger, self.offset)
            return True, []

        with self.ledger.open("rb") as stream:
            stream.seek(self.offset)
            appended = stream.read()

        complete, separator, _partial = appended.rpartition(b"\n")
        if not separator:
            return False, []

        self.offset += len(complete) + len(separator)
        self._baseline_digest = sha256_prefix(self.ledger, self.offset)
        entries: list[dict[str, Any]] = []
        for line in complete.splitlines():
            if not line.strip():
                continue
            decoded = json.loads(line.decode("utf-8"))
            if not isinstance(decoded, dict):
                raise ValueError("Totem ledger entries will be JSON objects")
            entries.append(decoded)
        return False, entries


def newest_entry(ledger: Path) -> dict[str, Any] | None:
    """Read the last complete JSONL entry without treating it as a new append."""
    entries = [line for line in ledger.read_text(encoding="utf-8").splitlines() if line.strip()]
    return json.loads(entries[-1]) if entries else None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Show appended ProntoPC Totem entries without modifying the ledger.")
    parser.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER, help="Totem JSONL ledger to observe")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--once", action="store_true", help="Show the newest existing Totem entry and exit")
    mode.add_argument("--watch", action="store_true", help="Monitor future append-only entries until interrupted")
    parser.add_argument("--poll-seconds", type=float, default=1.0, help="Watch polling interval (default: 1.0)")
    args = parser.parse_args(argv)

    if not args.ledger.is_file():
        print(f"LEDGER MISSING | {args.ledger}", file=sys.stderr)
        return 2
    if args.poll_seconds <= 0:
        parser.error("--poll-seconds will be greater than zero")

    if not args.watch:
        entry = newest_entry(args.ledger)
        if entry:
            print(format_new_append(entry))
        return 0

    monitor = TotemAppendMonitor(args.ledger)
    monitor.baseline()
    print(f"WATCHING TOTEM APPENDS | {args.ledger}")
    try:
        while True:
            reset, entries = monitor.read_appends()
            if reset:
                print("LEDGER RESET | baseline restored", file=sys.stderr)
            for entry in entries:
                print(format_new_append(entry), flush=True)
            time.sleep(args.poll_seconds)
    except KeyboardInterrupt:
        print("WATCH STOPPED")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
