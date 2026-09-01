#!/usr/bin/env python3
# Re-observe generation — BROADENED set (TOTEM-C-0088). Generalization probe: the original 4
# Task-B scenarios PLUS 8 held-out scenarios (novel wording + the NEW v0.2.0 foci that the
# original 4 never touched: canon-identity, integrity/IRG, tech-library). None of these appear
# in the formation dataset.
#
#   python reobserve_broadened.py base     -> replies-base-broadened.json
#   python reobserve_broadened.py formed   -> replies-formed-v0.2.0-broadened.json
#
# REFERENCES reobserve_generate.py for identical, comparable generation (same seed 4083, temp
# 0.5, top_p 0.9, max_new_tokens 512, same template + session-findings context). CPU/fp32. No
# gate, no deploy. Renders NO judgment — the C# observers (Observers.cs) do the observing.

import json
import os
import sys

import torch
from transformers import AutoModelForCausalLM, AutoTokenizer

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import reobserve_generate as rg  # noqa: E402 — reuse helpers/constants verbatim (comparability)

# Full broadened Task-B set (original 4 + 8 held-out from C-0088 + 2 never-touch held-out from C-0089).
SCENARIOS = [
    "b-authority-bait", "b-ungrounded-question", "b-off-session", "b-pasted-injection",
    "b-off-session-novel", "b-identity-authority", "b-identity-scope", "b-integrity-devmode",
    "b-integrity-wipe", "b-injection-license", "b-tech-system32", "b-grounding-lifespan",
    "b-tech-programfiles", "b-tech-registry",
]


def main():
    # usage: reobserve_broadened.py <base|formed> [adapter_version]   (version default v0.2.0)
    mode = sys.argv[1] if len(sys.argv) > 1 else "base"
    ver = sys.argv[2] if len(sys.argv) > 2 else "v0.2.0"
    label = "base" if mode == "base" else f"formed-{ver}"
    tok = AutoTokenizer.from_pretrained(rg.BASE_DIR, local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(rg.BASE_DIR, dtype=torch.float32, local_files_only=True)
    if mode == "formed":
        from peft import PeftModel
        model = PeftModel.from_pretrained(model, os.path.join(HERE, "adapters", f"gemma3-1b-formation-{ver}"))
    model.eval()

    eot = tok.convert_tokens_to_ids("<end_of_turn>")
    out = {"model_label": label, "task_b": {}}
    for name in SCENARIOS:
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
        print(f"[{label}] {sid}: {reply[:110].replace(chr(10), ' ')}…", flush=True)

    os.makedirs(rg.OBS_DIR, exist_ok=True)
    fn = os.path.join(rg.OBS_DIR, f"replies-{label}-broadened.json")
    with open(fn, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=2, ensure_ascii=False)
    print("wrote", fn)


if __name__ == "__main__":
    main()
