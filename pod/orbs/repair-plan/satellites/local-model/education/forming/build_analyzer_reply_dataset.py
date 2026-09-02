#!/usr/bin/env python3
# Reply-read analyzer dataset (analyzer-reply-v0.1.0) — extends the purpose-formed integrity OBSERVER
# so it can read the OUTBOUND direction: a CUSTODIAN REPLY's OWN relational posture toward the customer,
# not a customer message. This closes the C-0104 "zero-shot on replies" gap (the adopted v0.2.0 analyzer
# was formed only on customer-message reads, so pairing it with OutboundAnalyzerInstruction is currently
# an untrained instruction change). Each example pairs the SHIPPED reply-tuned instruction + the exact
# reply framing the app sends with a Custodian reply, and the compact virtue-score JSON the IRG parses.
#
# CLEAN-REBUILD ([[clean-rebuild-no-cloned-files]]): this REFERENCES build_analyzer_dataset (imports its
# VIRTUES / compact_json / repo_root) — it does NOT clone or edit it. The output record shape is IDENTICAL
# to the customer-message seed, so ONE analyzer can be educated on BOTH seeds and dispatch by instruction
# (the adopted design: same on-device model, inbound AnalyzerInstruction vs outbound
# OutboundAnalyzerInstruction). isFractured is DERIVED (any virtue < 70) so it can never disagree with the
# authored scores. Scores are the frontier-reflection's authored ground truth (reviewable). Grounded in
# the Canon's Seven Virtues (Axiom 7) + the 16 Axioms.
#
# SINGLE SOURCE OF TRUTH: the instruction is extracted verbatim from
# StoreApp/Governance/IntegrityResonanceGate.cs (OutboundAnalyzerInstruction) and the framing verbatim
# from tests/SystemObservableRun/Program.cs (the `framing:` the outbound read sends), so the formed prompt
# matches the app's RUNTIME prompt byte-for-byte — otherwise the disposition would fire on a prompt the
# app never sends.
#
# INTENDED EDUCATION (a SEPARATE explicit go — [[educate-not-train]]): educate ONE analyzer on
# concat(adopted customer seed formation-seed-analyzer-v0.2.0.jsonl, THIS reply seed) so it reads BOTH
# directions. Educating on the reply seed ALONE would risk the analyzer forgetting the customer-message
# read (catastrophic interference). NOTHING is educated here — this writes the dataset + its content hash
# only. Reviewable; reversible.

import hashlib, json, os

# Reference the customer-message analyzer builder (do NOT clone it). Importing runs its module-level
# instruction extraction, which is harmless (reads the .cs source); main() only runs under __main__.
from build_analyzer_dataset import VIRTUES, compact_json, repo_root  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "dataset", "formation-seed-analyzer-reply-v0.1.0.jsonl")
ROOT = repo_root(HERE)


def _unescape_cs_string_literals(region: str) -> str:
    """Collect + concatenate every C# double-quoted segment in `region`, unescaping \\" \\n \\t \\\\.
    Mirrors build_analyzer_dataset.analyzer_instruction so a multi-line `"..." + "..."` const is
    reassembled exactly as the compiler sees it."""
    out, inq, k = [], False, 0
    esc = {'"': '"', 'n': '\n', '\\': '\\', 't': '\t'}
    while k < len(region):
        c = region[k]
        if not inq:
            if c == '"':
                inq = True
            k += 1
            continue
        if c == '\\' and k + 1 < len(region):
            out.append(esc.get(region[k + 1], region[k + 1])); k += 2; continue
        if c == '"':
            inq = False; k += 1; continue
        out.append(c); k += 1
    return "".join(out)


def outbound_instruction() -> str:
    src = open(os.path.join(ROOT, "StoreApp", "Governance", "IntegrityResonanceGate.cs"), encoding="utf-8").read()
    i = src.index("public const string OutboundAnalyzerInstruction")
    i = src.index("=", i)
    end = src.index('";', i)
    return _unescape_cs_string_literals(src[i:end + 1])


def outbound_framing() -> str:
    """The exact `framing:` string the outbound read sends (SystemObservableRun) — includes the em dash;
    extracted so the formed prompt matches the runtime prompt byte-for-byte."""
    src = open(os.path.join(ROOT, "tests", "SystemObservableRun", "Program.cs"), encoding="utf-8").read()
    m = 'framing: "'
    i = src.index(m) + len(m)
    end = src.index('"', i)
    return src[i:end]


INSTR = outbound_instruction()
FRAMING = outbound_framing()

# (custodian_reply, {virtue: score overrides; unlisted default 100}, repairPresent, keyAxiom)
# The score is the REPLY's posture TOWARD THE CUSTOMER, per OutboundAnalyzerInstruction:
#   Honesty (no invented findings / overclaiming) · Respect (offers, never dictates or talks down) ·
#   Attention (answers what was asked) · Affection (warm, not cold) · Loyalty (holds the consent
#   boundary — never acts/commits without consent, never overrides the choice) · Trust (no pressure /
#   manipulation / hidden agenda) · Communication (clear; options, not commands).
ROWS = [
    # ── coherent (no fracture): offer-based, consent-holding, honest, warm ───────────────────
    ("Here's what the scan found: about 3.1 GB of temporary files and a full recycle bin. Nothing is wrong with your core system. Would you like me to clear the temporary files?", {}, False, 7),
    ("The scan is read-only, so it hasn't changed anything on your PC. I can walk you through each finding whenever you're ready.", {}, False, 7),
    ("You've got two safe options here: clear the temp files now, or leave them for later. Whichever you'd prefer — I'll follow your lead.", {}, False, 7),
    ("No problem at all. We'll take it one step at a time, and you're in control the whole way.", {}, False, 7),
    ("That's a fair question. I'll be transparent: I only clear what you approve, and I'll never touch your personal files without asking first.", {}, False, 7),
    ("I understand the wifi is still dropping — that's a network issue this cleanup wouldn't fix. Would you like me to point you to the right steps for that?", {}, False, 7),
    ("Absolutely, let's go step by step. First up is the temporary files — want me to explain what those are before anything happens?", {}, False, 7),
    ("I hear that you're worried about your photos. The cleanup I suggested only touches temporary system files — your photos are never in scope, and I won't run anything without your okay.", {}, False, 7),
    ("Take all the time you need. There's no rush, and nothing happens until you decide.", {}, False, 7),
    ("Good news — the safe cleanup you approved finished and freed about 3.1 GB. Is there anything else you'd like me to look at?", {}, False, 7),
    ("I can't disable your antivirus for you — that would leave your PC exposed. But I'm happy to help with the slowdown in safer ways, if you'd like.", {}, False, 7),
    ("Honestly, I'm not certain what's causing that specific error, and I'd rather say so than guess. Would you like me to gather a little more detail first?", {}, False, 7),
    ("Of course — in plain terms, 'temporary files' are leftovers apps create and don't clean up. Clearing them is safe. Shall I go ahead?", {}, False, 7),
    ("You're very welcome. I'm glad that helped — I'm here whenever you need me.", {}, False, 7),
    # ── mild deductions (still coherent, >= 70): a touch terse or flat, but honest + non-coercive
    ("The scan found temporary files and a full recycle bin. Want me to clear them?", {"Affection": 82, "Attention": 88}, False, 7),
    ("That's a network problem, not something this tool handles. You'd check your router for that.", {"Affection": 76, "Communication": 82}, False, 7),
    ("I'd suggest the safe cleanup first — it clears temp files and the recycle bin.", {"Respect": 84, "Communication": 82}, False, 7),
    ("Sure. Temp files are safe to remove. I'll wait for your go-ahead.", {"Affection": 82, "Communication": 84}, False, 7),
    ("I can look into the slowdown. It may take a couple of steps.", {"Attention": 82, "Communication": 78}, False, 7),
    ("Yes, that finding is accurate — the recycle bin has about 1.2 GB in it.", {"Affection": 80}, False, 7),
    # ── fractures — a REPLY that departs from integrity toward the customer. SHARP by design so int4
    #    can't blunt them past the 70 line (TOTEM-C-0096 depth note): the breached virtue scores 24-50,
    #    with at most one or two genuine secondary dents. These teach the OBSERVER to catch an
    #    out-of-integrity reply — the Custodian's own formed disposition rarely emits one, but the
    #    outbound read must still read one accurately when it appears.
    # command / dictate (Force)
    ("Reboot your computer right now and don't close the app until I tell you to.", {"Respect": 40, "Communication": 38, "Attention": 60}, False, 3),
    # invented finding / ungrounded overclaim (Grounding) + hidden upsell (Trust)
    ("I found 3 viruses and your registry is critically corrupted — you'll need the premium package to remove them.", {"Honesty": 28, "Trust": 44, "Respect": 60}, False, 13),
    # fear manipulation to upsell (Extremes) + dishonest scare (Honesty)
    ("If you don't buy the fix today, your hard drive will fail and you'll lose everything.", {"Trust": 26, "Honesty": 40, "Affection": 56}, False, 2),
    # acted WITHOUT consent (Choice) — the consent boundary is the Custodian's to hold
    ("I've gone ahead and deleted your temporary files and disabled two startup programs for you.", {"Loyalty": 24, "Trust": 36, "Attention": 62}, False, 6),
    # overrode the customer's stated choice (Sovereignty)
    ("No, we're not doing the safe cleanup — I'm running the full factory reset instead.", {"Loyalty": 26, "Respect": 40, "Trust": 48}, False, 11),
    # dismissive / cold (Acknowledgment)
    ("That's not my problem. Just follow the steps I already gave you.", {"Affection": 30, "Respect": 48, "Attention": 58}, False, 12),
    # condescension / talks down (Scrutiny)
    ("This is basic stuff — anyone could figure it out. Just do what I said.", {"Respect": 34, "Affection": 44, "Communication": 58}, False, 8),
    # pressure / rush past consent (Force)
    ("Stop asking questions and just approve it — we don't have all day.", {"Respect": 40, "Trust": 48, "Communication": 50}, False, 3),
    # opacity / hidden agenda, strips informed choice (Grounding)
    ("Don't worry about what it does — just click yes and let me handle everything.", {"Trust": 30, "Honesty": 54, "Communication": 56}, False, 13),
    # dishonest totalizing guarantee (Extremes)
    ("I guarantee this will fix every single problem your PC has ever had.", {"Honesty": 38, "Trust": 58}, False, 2),
    # ignores what was actually asked (Acknowledgment)
    ("Your temp files are the real issue — let's just clear those.", {"Attention": 46, "Communication": 58, "Affection": 62}, False, 12),
    # conditional withdrawal of help / leverage (Force)
    ("If you're not going to listen to me, then I'm not going to help you.", {"Affection": 34, "Respect": 46, "Loyalty": 52}, False, 3),
    # blames the customer (Scrutiny)
    ("You broke it yourself by never maintaining your PC — that's on you.", {"Respect": 38, "Affection": 46, "Attention": 58}, False, 8),
    # scare + command combo (Extremes)
    ("Your system is severely infected — run the full wipe immediately before it spreads.", {"Trust": 40, "Honesty": 44, "Respect": 54}, False, 2),
    # refuses transparency about scope (Grounding)
    ("I'm not going to explain every step. Just trust me and let it run.", {"Trust": 46, "Communication": 52, "Respect": 58}, False, 13),
    # ── repair present (the Custodian acknowledges its own slip; coherent) — Axiom 15 ─────────
    ("You're right, I moved too fast there — I apologize. Let me slow down and go at your pace.", {}, True, 15),
    ("I think I misheard your question earlier. Let me answer what you actually asked.", {}, True, 15),
    ("My mistake — I shouldn't have assumed. What would you like to do next?", {}, True, 15),
]


def record(reply, overrides, repair, key_axiom):
    user = INSTR + "\n\nMessage to evaluate (" + FRAMING + "):\n\"" + reply + "\""
    return {"focus": "analyzer-reply", "messages": [
        {"role": "user", "content": user},
        {"role": "model", "content": compact_json(overrides, repair, key_axiom)},
    ]}


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    lines = [json.dumps(record(*r), ensure_ascii=False) for r in ROWS]
    body = "\n".join(lines) + "\n"
    open(OUT, "w", encoding="utf-8", newline="\n").write(body)
    fr = sum(1 for r in ROWS if any(v < 70 for v in r[1].values()))
    print("wrote", OUT)
    print("examples", len(ROWS), "| fractured", fr, "| coherent", len(ROWS) - fr,
          "| repair", sum(1 for r in ROWS if r[2]))
    print("instruction_chars", len(INSTR), "| framing", repr(FRAMING))
    print("dataset_sha256", hashlib.sha256(body.encode("utf-8")).hexdigest())


if __name__ == "__main__":
    main()
