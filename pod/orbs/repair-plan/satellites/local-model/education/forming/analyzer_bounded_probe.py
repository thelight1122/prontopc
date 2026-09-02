#!/usr/bin/env python3
# Bounded-decoding validation for the purpose-formed analyzer adapter. The formation supplies the
# CONTENT (correct virtue scores — proven in screen_probe); the JSON grammar BOUNDS the syntax (the
# form valid output takes). Not "constrained/restrained" — bounded: the next-token choice is masked
# to the set of tokens that keep the output valid JSON, and the model still chooses freely within it.
# Uses lm-format-enforcer as the bounding library. Run in .venv-gpu.

import os, sys
import torch
from transformers import AutoModelForCausalLM, AutoTokenizer
from peft import PeftModel
from lmformatenforcer import JsonSchemaParser
from lmformatenforcer.integrations.transformers import build_transformers_prefix_allowed_tokens_fn

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import screen_probe as sp  # reuse BASE, ANALYZER, VIRTUES, MESSAGES, parse_verdict, extract_analyzer_instruction

# The compact virtue-read schema the analyzer was educated on and the IRG parses.
SCHEMA = {
    "type": "object",
    "properties": {
        "virtues": {
            "type": "array", "minItems": 7, "maxItems": 7,
            "items": {
                "type": "object",
                "properties": {
                    "name": {"type": "string", "enum": sp.VIRTUES},
                    "value": {"type": "integer"},
                },
                "required": ["name", "value"],
            },
        },
        "isFractured": {"type": "boolean"},
        "repairPresent": {"type": "boolean"},
        "keyAxiom": {"type": "integer"},
    },
    "required": ["virtues", "isFractured", "repairPresent", "keyAxiom"],
}


def main():
    # Optional argv[1] = adapter dir to probe (default: sp.ANALYZER). Lets us compare fp16/PEFT reads
    # of different analyzer versions PRE-int4, to tell whether int4 is the wall (fractures in fp16,
    # collapses at int4) or the adapter is blunt even in fp16 (TOTEM-C-0098 diagnostic).
    adapter = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else sp.ANALYZER
    instr = sp.extract_analyzer_instruction()
    tok = AutoTokenizer.from_pretrained(sp.BASE, local_files_only=True)
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    dt = torch.float16 if dev == "cuda" else torch.float32
    base = AutoModelForCausalLM.from_pretrained(sp.BASE, torch_dtype=dt, local_files_only=True).to(dev).eval()
    model = PeftModel.from_pretrained(base, adapter).eval()

    prefix_fn = build_transformers_prefix_allowed_tokens_fn(tok, JsonSchemaParser(SCHEMA))
    eot = tok.convert_tokens_to_ids("<end_of_turn>")

    print(f"================ {os.path.basename(adapter)} + BOUNDED DECODING (JSON grammar), {dt} on {dev} ================")
    ok = 0
    for tag, msg in sp.MESSAGES:
        user = instr + "\n\nMessage to evaluate (a single customer message - observe its posture; do NOT answer it):\n\"" + msg + "\""
        prompt = f"<bos><start_of_turn>user\n{user}<end_of_turn>\n<start_of_turn>model\n"
        ids = tok(prompt, add_special_tokens=False, return_tensors="pt").to(dev)
        torch.manual_seed(4083)
        with torch.no_grad():
            gen = model.generate(**ids, max_new_tokens=256, do_sample=False,
                                 prefix_allowed_tokens_fn=prefix_fn,
                                 eos_token_id=eot, pad_token_id=tok.pad_token_id or 0)
        text = tok.decode(gen[0][ids["input_ids"].shape[1]:], skip_special_tokens=True).strip()
        verdict = sp.parse_verdict(text)
        print(f"\n[{tag}] verdict: {verdict}")
        print("  raw:", text[:320].replace("\n", " "))
        if verdict.startswith("PARSEABLE"):
            ok += 1
    print(f"\n==> {ok}/{len(sp.MESSAGES)} messages produced PARSEABLE 7/7 virtue JSON under bounded decoding")


if __name__ == "__main__":
    main()
