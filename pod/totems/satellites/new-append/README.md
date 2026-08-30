# Totem New-Append Satellite

This self-contained, read-only Satellite observes only `pod/totems/totems.jsonl`.
It does not write a cursor, alter a Totem, or import the application.

```powershell
# Show the newest existing record
python pod/totems/satellites/new-append/totem_new_append_satellite.py --once

# Watch future records; each arrival is displayed as NEW APPEND | TOTEM-XXXX | timestamp
python pod/totems/satellites/new-append/totem_new_append_satellite.py --watch
```

The watcher baselines at startup, so historical entries are not falsely announced.
If the ledger shrinks or any baseline bytes change, it emits `LEDGER RESET` and re-baselines
rather than interpreting a rewrite as an append.
