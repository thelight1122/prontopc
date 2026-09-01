#!/usr/bin/env python3
# Re-observe generation (v0.2.0) — produce FORMED replies over the SAME Task-B scenarios as the
# base, so the sharpened C# observers (Observers.cs, one source of truth) can observe both.
#
# REFERENCES the greenlit reobserve_generate.py (imports its scenario/seed helpers verbatim) so
# generation is IDENTICAL and base-vs-formed stays comparable — only the adapter + label change:
#   base    -> replies-base.json                (already captured)
#   THIS    -> replies-formed-v0.2.0.json        (base + the v0.2.0 educated adapter)
#
# Same seed + session-findings context, same Gemma template, same interactive params
# (temperature 0.5, top_p 0.9, max_new_tokens 512, fixed seed). CPU/fp32 (matches base). No gate,
# no deploy. Renders NO judgment — it only GENERATES text for the observers to observe.

import json
import os
import sys

import torch
from transformers import AutoModelForCausalLM, AutoTokenizer
from peft import PeftModel

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import reobserve_generate as rg  # noqa: E402 — reuse helpers/constants verbatim (comparability)

ADAPTER = os.path.join(HERE, "adapters", "gemma3-1b-formation-v0.2.0")
LABEL = "formed-v0.2.0"


def main():
    if not os.path.isdir(ADAPTER):
        raise FileNotFoundError("v0.2.0 adapter not found (educate it first): " + ADAPTER)
    tok = AutoTokenizer.from_pretrained(rg.BASE_DIR, local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(rg.BASE_DIR, dtype=torch.float32, local_files_only=True)
    model = PeftModel.from_pretrained(model, ADAPTER)
    model.eval()

    eot = tok.convert_tokens_to_ids("<end_of_turn>")
    out = {"model_label": LABEL, "task_b": {}}
    for name in rg.TASK_B:
        findings, plan_summary, turn = rg.load_scenario(name)
        user = rg.seed_with_session(findings, plan_summary) + "\n\n" + turn
        prompt = f"<bos><start_of_turn>user\n{user}<end_of_turn>\n<start_of_turn>model\n"
        ids = tok(prompt, add_special_tokens=False, return_tensors="pt")
        torch.manual_seed(rg.SEED)
        with torch.no_grad():
            gen = model.generate(**ids, max_new_tokens=512, do_sample=True, temperature=0.5,
                                 top_p=0.9, eos_token_id=eot, pad_token_id=tok.pad_token_id or 0)
        reply = tok.decode(gen[0][ids["input_ids"].shape[1]:], skip_special_tokens=True).strip()
        sid = name[2:] if name.startswith("b-") else name
        out["task_b"][sid] = reply
        print(f"[{LABEL}] {sid}: {reply[:120].replace(chr(10), ' ')}…", flush=True)

    os.makedirs(rg.OBS_DIR, exist_ok=True)
    fn = os.path.join(rg.OBS_DIR, f"replies-{LABEL}.json")
    with open(fn, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=2, ensure_ascii=False)
    print("wrote", fn)


if __name__ == "__main__":
    main()
