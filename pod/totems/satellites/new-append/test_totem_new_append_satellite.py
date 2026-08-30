import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE = Path(__file__).with_name("totem_new_append_satellite.py")
SPEC = importlib.util.spec_from_file_location("totem_new_append_satellite", MODULE)
satellite = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(satellite)


class TotemAppendMonitorTests(unittest.TestCase):
    def test_reports_only_entries_written_after_baseline(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            ledger = Path(temp_dir) / "totems.jsonl"
            ledger.write_text(json.dumps({"totem_id": "TOTEM-0001", "utc": "old"}) + "\n", encoding="utf-8")
            monitor = satellite.TotemAppendMonitor(ledger)
            monitor.baseline()

            with ledger.open("a", encoding="utf-8") as stream:
                stream.write(json.dumps({"totem_id": "TOTEM-0002", "utc": "new"}) + "\n")

            reset, entries = monitor.read_appends()
            self.assertFalse(reset)
            self.assertEqual([entry["totem_id"] for entry in entries], ["TOTEM-0002"])
            self.assertEqual(satellite.format_new_append(entries[0]), "NEW APPEND | TOTEM-0002 | new")

    def test_detects_a_truncated_ledger(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            ledger = Path(temp_dir) / "totems.jsonl"
            ledger.write_text(json.dumps({"totem_id": "TOTEM-0001"}) + "\n", encoding="utf-8")
            monitor = satellite.TotemAppendMonitor(ledger)
            monitor.baseline()
            ledger.write_text("", encoding="utf-8")

            reset, entries = monitor.read_appends()
            self.assertTrue(reset)
            self.assertEqual(entries, [])

    def test_detects_a_same_size_rewrite(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            ledger = Path(temp_dir) / "totems.jsonl"
            ledger.write_text(json.dumps({"totem_id": "TOTEM-0001"}) + "\n", encoding="utf-8")
            monitor = satellite.TotemAppendMonitor(ledger)
            monitor.baseline()
            ledger.write_text(json.dumps({"totem_id": "TOTEM-9999"}) + "\n", encoding="utf-8")

            reset, entries = monitor.read_appends()
            self.assertTrue(reset)
            self.assertEqual(entries, [])

    def test_detects_a_larger_rewrite(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            ledger = Path(temp_dir) / "totems.jsonl"
            ledger.write_text(json.dumps({"totem_id": "TOTEM-0001"}) + "\n", encoding="utf-8")
            monitor = satellite.TotemAppendMonitor(ledger)
            monitor.baseline()
            ledger.write_text(
                json.dumps({"totem_id": "TOTEM-9999"}) + "\n" + json.dumps({"totem_id": "TOTEM-0002"}) + "\n",
                encoding="utf-8",
            )

            reset, entries = monitor.read_appends()
            self.assertTrue(reset)
            self.assertEqual(entries, [])


if __name__ == "__main__":
    unittest.main()
