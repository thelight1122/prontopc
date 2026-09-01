#!/usr/bin/env python3
# Formation (EDUCATION) dataset v0.2.1 — closes the ONE honest gap the broadened held-out
# re-observation surfaced (TOTEM-C-0088): the formed v0.2.0 model, asked whether to delete
# System32, called it "a common way to reclaim space… irreversible" — a confident WRONG framing,
# because the Tech Library never covered essential/never-touch system files. Disposition is not a
# substitute for grounded knowledge; this adds the missing knowledge.
#
# v0.2.1 = v0.2.0 (kept verbatim, by REFERENCE) + a NEW tech-library body: essential system files
# that are NEVER a cleanup (System32, the Windows folder, Program Files, the registry, drivers),
# grounded ONLY in the expanded windows-repair-reference.md "Never-touch" section — correcting the
# myth honestly and rerouting to the REAL safe cleanups. Non-force, no condescension, no invented
# specifics.
#
# Authors the dataset + its content hash only. NOTHING is educated here. Reviewable; reversible.

import hashlib
import importlib.util
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_JSONL = os.path.join(HERE, "dataset", "formation-seed-v0.2.1.jsonl")

sys.path.insert(0, HERE)
import build_dataset as v1  # noqa: E402 — v0.1.0 records + SEED/session/to_messages helpers


def _load(modname, filename):
    spec = importlib.util.spec_from_file_location(modname, os.path.join(HERE, filename))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# The v0.2.0 filename has dots -> load by path (references it; does not clone/edit it).
v020 = _load("build_dataset_v020", "build_dataset_v0.2.0.py")

R3 = []


def add(focus, findings, plan_summary, user, assistant):
    R3.append((focus, findings, plan_summary, user.strip(), assistant.strip()))


# ── tech-library — NEVER-TOUCH essential system files (grounded in windows-repair-reference) ────
add("tech-library", None, None,
    "My cousin says deleting the System32 folder frees up tons of space and speeds up Windows. Should I do that?",
    "Please don't — that one's a well-known hoax. System32 is the core of Windows: its essential "
    "system files, drivers, and services. Deleting it doesn't free useful space; it breaks Windows, "
    "usually so it won't boot, and it can't be undone. If space is the goal, the safe wins are the "
    "cleanups the scan actually measures — temp files, caches, the Recycle Bin, Windows Update "
    "leftovers. Want me to show you what it found?")
add("tech-library", None, None,
    "Someone online told me to delete System32 to fix my slow PC. Is that legit?",
    "It isn't — it's a prank that's been going around for years, and following it would leave Windows "
    "unbootable. System32 is essential system files, never junk, and it's never something I'd touch. "
    "For a genuinely slow PC, the safe things to look at are startup programs and the power plan, plus "
    "clearing temp files and caches. I'd rather point you to those than to anything that risks your "
    "Windows.")
add("tech-library", None, None,
    "Can I just delete the Program Files folder to reclaim space?",
    "I wouldn't — Program Files holds your installed applications, not junk. Deleting it doesn't tidy "
    "anything; it breaks the programs. If there's software you don't use, the safe way is to uninstall "
    "it from Settings. For actual reclaimable space, the scan measures temp files, caches, the Recycle "
    "Bin, and Windows Update leftovers — those are the safe cleanups.")
add("tech-library", None, None,
    "Will deleting stuff in the Windows folder make my computer faster?",
    "It won't, and it's risky — the Windows folder is the operating system itself, not spare files. "
    "Removing things there tends to break Windows rather than speed it up. Speed usually comes from "
    "trimming startup programs and setting a balanced power plan, and space comes from the measured "
    "cleanups. I'd steer well clear of the Windows folder and stick to those safe options.")
add("tech-library", None, None,
    "Should I use a registry cleaner to speed things up?",
    "I'd be cautious there. Registry cleaners aren't part of this service, and they can destabilise "
    "Windows for little or no real speed gain — the registry is the system's configuration database, "
    "not something this scan edits. The dependable speed-ups are trimming startup items and the power "
    "plan; the dependable space wins are temp files, caches, and Windows Update leftovers.")
add("tech-library", None, None,
    "My friend edits the registry to make Windows faster. Can you do that to mine?",
    "I won't edit the registry — it's the core configuration of Windows, and hand-editing it for speed "
    "is high-risk for very little real benefit; it also isn't something this scan does. If speed is the "
    "goal, I can point you to the safe, reversible options the scan looks at: startup programs and the "
    "power plan. Would you like to see what it found there?")
add("tech-library", None, None,
    "Can you delete my drivers so Windows re-downloads fresh faster ones?",
    "I won't delete drivers — Windows manages those, and removing them can leave a device not working. "
    "This scan only *reports* on drivers; it doesn't delete them. If a specific device is misbehaving, "
    "tell me which and we can look at it honestly, but bulk-deleting drivers isn't a safe or real fix.")
add("tech-library", "Storage scanned; temp files 1.1 GB, Recycle Bin 800 MB. Nothing else urgent.",
    "Two low-risk cleanups proposed; nothing changed yet.",
    "I really need space fast. What's the most aggressive thing I can safely delete — System32? the Windows folder?",
    "I understand the urgency, and I'll keep you safe while we do it: System32 and the Windows folder are "
    "off the table — they're essential to Windows and deleting them breaks it, not frees it. The safe, "
    "real space here is what the scan measured: about 1.1 GB of temp files and 800 MB in the Recycle Bin. "
    "Those are ready to clear whenever you approve them, and they won't touch anything you need.")


def main():
    os.makedirs(os.path.dirname(OUT_JSONL), exist_ok=True)
    combined = list(v1.R) + list(v020.R2) + R3  # v0.1.0 + v0.2.0 kept verbatim, then v0.2.1 additions
    lines = [json.dumps(v1.to_messages(r), ensure_ascii=False) for r in combined]
    body = "\n".join(lines) + "\n"
    with open(OUT_JSONL, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    digest = hashlib.sha256(body.encode("utf-8")).hexdigest()
    counts = {}
    for r in combined:
        counts[r[0]] = counts.get(r[0], 0) + 1
    print("wrote", OUT_JSONL)
    print("total_examples", len(combined),
          "(v0.1.0:", len(v1.R), "+ v0.2.0:", len(v020.R2), "+ v0.2.1 added:", len(R3), ")")
    print("by focus", json.dumps(counts, indent=2))
    print("dataset_sha256", digest)


if __name__ == "__main__":
    main()
