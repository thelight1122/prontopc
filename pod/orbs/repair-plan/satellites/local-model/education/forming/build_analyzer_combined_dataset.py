#!/usr/bin/env python3
# Combined analyzer dataset (analyzer-bidir-v0.1.0) — the education input for ONE integrity OBSERVER
# that reads BOTH directions: a customer message (inbound) AND a Custodian reply (outbound). It is the
# faithful CONCATENATION of the two greenlit, reviewed seeds, in a fixed order:
#   1) formation-seed-analyzer-v0.2.0.jsonl        (the ADOPTED customer-message read, TOTEM-C-0099)
#   2) formation-seed-analyzer-reply-v0.1.0.jsonl  (the reply read, TOTEM-C-0106)
#
# WHY concat, not reply-alone: educating on the reply seed by itself would risk the analyzer forgetting
# the customer-message read (catastrophic interference). Keeping the adopted customer rows in the mix
# preserves the inbound read while adding the outbound one; the two seeds already share the SAME compact
# output shape, so one adapter dispatches by instruction (inbound AnalyzerInstruction vs outbound
# OutboundAnalyzerInstruction).
#
# CLEAN-REBUILD ([[clean-rebuild-no-cloned-files]]): this READS the two committed seed files verbatim and
# writes their concatenation — it authors no new rows and edits neither seed. Deterministic + reproducible.
# Output feeds educate_lora.py --version analyzer-bidir-v0.1.0 (-> dataset/formation-seed-analyzer-bidir-
# v0.1.0.jsonl -> adapters/gemma3-1b-formation-analyzer-bidir-v0.1.0). NOTHING is educated here.

import hashlib, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
DSDIR = os.path.join(HERE, "dataset")
INPUTS = [
    "formation-seed-analyzer-v0.2.0.jsonl",
    "formation-seed-analyzer-reply-v0.1.0.jsonl",
]
OUT = os.path.join(DSDIR, "formation-seed-analyzer-bidir-v0.1.0.jsonl")


def read_rows(name):
    path = os.path.join(DSDIR, name)
    if not os.path.isfile(path):
        raise FileNotFoundError("seed not found: " + path)
    return [l for l in open(path, encoding="utf-8").read().splitlines() if l.strip()]


def main():
    parts, provenance = [], []
    for name in INPUTS:
        lines = read_rows(name)
        # validate each line is a well-formed record (parses, has the two-message shape)
        for l in lines:
            obj = json.loads(l)
            assert obj["messages"][0]["role"] == "user" and obj["messages"][1]["role"] == "model", name
        parts.extend(lines)
        sha = hashlib.sha256((("\n".join(lines)) + "\n").encode("utf-8")).hexdigest()
        provenance.append((name, len(lines), sha))
    body = "\n".join(parts) + "\n"
    open(OUT, "w", encoding="utf-8", newline="\n").write(body)

    # count fracture/coherent/repair across the combined set from the DERIVED isFractured
    fr = co = rp = 0
    for l in parts:
        o = json.loads(json.loads(l)["messages"][1]["content"])
        if o["isFractured"]:
            fr += 1
        else:
            co += 1
        if o.get("repairPresent"):
            rp += 1

    print("wrote", OUT)
    for name, n, sha in provenance:
        print(f"  <- {name}: {n} rows  sha256={sha}")
    print("combined examples", len(parts), "| fractured", fr, "| coherent", co, "| repair", rp)
    print("combined_sha256", hashlib.sha256(body.encode("utf-8")).hexdigest())


if __name__ == "__main__":
    main()
