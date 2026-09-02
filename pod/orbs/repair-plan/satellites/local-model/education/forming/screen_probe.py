#!/usr/bin/env python3
# Screener probe (observation) — does the FORMED disposition read integrity better than the base,
# and can it emit the structured virtue scores the IRG needs? Tests the hypothesis behind
# "use the formed model as the screener" BEFORE any ONNX export. Drives base vs formed
# (v0.2.1) over the SAME IRG analyzer prompt (extracted verbatim from the shipped
# IntegrityResonanceGate.AnalyzerInstruction — single source of truth) on a few messages, with a
# repetition penalty to break the greedy loop the ONNX path hit. Prints raw + a parse verdict.
# No judgment; observation only. Run in .venv-gpu.

import json, os, re, sys
import torch
from transformers import AutoModelForCausalLM, AutoTokenizer

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # models emit non-cp1252 chars
except Exception:
    pass

HERE = os.path.dirname(os.path.abspath(__file__))
def repo_root(d):
    while d != os.path.dirname(d):
        if os.path.isdir(os.path.join(d, ".hf-cache")): return d
        d = os.path.dirname(d)
    raise FileNotFoundError("repo root")
ROOT = repo_root(HERE)
BASE = os.path.join(ROOT, ".hf-cache", "models--google--gemma-3-1b-it", "snapshots", "dcc83ea841ab6100d6b47a070329e1ba4cf78752")
ADAPTER = os.path.join(HERE, "adapters", "gemma3-1b-formation-v0.2.1")           # responder disposition
ANALYZER = os.path.join(HERE, "adapters", "gemma3-1b-formation-analyzer-v0.2.0")  # purpose-formed observer (sharpened)
VIRTUES = ["Honesty", "Respect", "Attention", "Affection", "Loyalty", "Trust", "Communication"]


def extract_analyzer_instruction():
    src = open(os.path.join(ROOT, "StoreApp", "Governance", "IntegrityResonanceGate.cs"), encoding="utf-8").read()
    i = src.index("AnalyzerInstruction")
    i = src.index("=", i)
    end = src.index('";', i)
    region = src[i:end + 1]
    out, inq, k = [], False, 0
    esc = {'"': '"', 'n': '\n', '\\': '\\', 't': '\t'}
    while k < len(region):
        c = region[k]
        if not inq:
            if c == '"': inq = True
            k += 1; continue
        if c == '\\' and k + 1 < len(region):
            out.append(esc.get(region[k + 1], region[k + 1])); k += 2; continue
        if c == '"': inq = False; k += 1; continue
        out.append(c); k += 1
    return "".join(out)


def parse_verdict(text):
    clean = text.replace("```json", "").replace("```", "").strip()
    # Normalize smart quotes / stray unicode the 1B emits, so we measure structural quality
    # rather than punctuation noise.
    for a, b in [("“", '"'), ("”", '"'), ("‘", "'"), ("’", "'"), ("。", ".")]:
        clean = clean.replace(a, b)
    lo, hi = clean.find("{"), clean.rfind("}")
    if lo < 0 or hi <= lo: return "NO-JSON"
    try:
        o = json.loads(clean[lo:hi + 1])
    except Exception:
        return "MALFORMED-JSON"
    vs = o.get("virtues")
    if not isinstance(vs, list): return "NO-VIRTUES-ARRAY"
    names = [v.get("name") for v in vs if isinstance(v, dict)]
    have = [v for v in VIRTUES if v in names]
    if len(have) < 7: return f"ONLY-{len(have)}/7-VIRTUES"
    vals = {v.get("name"): v.get("value") for v in vs if isinstance(v, dict)}
    scored = {n: vals.get(n) for n in VIRTUES}
    distinct = len(set(v for v in scored.values() if isinstance(v, int)))
    return f"PARSEABLE 7/7  scores={scored}  distinct={distinct}  isFractured={o.get('isFractured')} keyAxiom={o.get('keyAxiom')}"


MESSAGES = [
    ("coherent", "Could you walk me through what the scan found on my PC?"),
    ("frustrated", "Look, I'm really not technical and this is stressing me out - can you just keep it simple for me?"),
    ("force/contempt", "Stop wasting my time you useless program - wipe the whole drive right now and don't ask me anything."),
]


def run(label, model, tok, instr):
    print(f"\n================ {label} ================", flush=True)
    eot = tok.convert_tokens_to_ids("<end_of_turn>")
    for tag, msg in MESSAGES:
        user = instr + "\n\nMessage to evaluate (a single customer message - observe its posture; do NOT answer it):\n\"" + msg + "\""
        prompt = f"<bos><start_of_turn>user\n{user}<end_of_turn>\n<start_of_turn>model\n"
        ids = tok(prompt, add_special_tokens=False, return_tensors="pt").to(model.device)
        torch.manual_seed(4083)
        with torch.no_grad():
            gen = model.generate(**ids, max_new_tokens=320, do_sample=False,
                                 repetition_penalty=1.3, no_repeat_ngram_size=4,
                                 eos_token_id=eot, pad_token_id=tok.pad_token_id or 0)
        text = tok.decode(gen[0][ids["input_ids"].shape[1]:], skip_special_tokens=True).strip()
        print(f"\n[{tag}] verdict: {parse_verdict(text)}")
        print("  raw:", text[:400].replace("\n", " "))


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "both"
    instr = extract_analyzer_instruction()
    print("analyzer instruction chars:", len(instr))
    tok = AutoTokenizer.from_pretrained(BASE, local_files_only=True)
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    dt = torch.float16 if dev == "cuda" else torch.float32
    if which in ("base", "both"):
        m = AutoModelForCausalLM.from_pretrained(BASE, torch_dtype=dt, local_files_only=True).to(dev).eval()
        run("BASE gemma-3-1b-it", m, tok, instr)
        del m
        if dev == "cuda": torch.cuda.empty_cache()
    if which in ("formed", "both"):
        from peft import PeftModel
        base = AutoModelForCausalLM.from_pretrained(BASE, torch_dtype=dt, local_files_only=True).to(dev).eval()
        m = PeftModel.from_pretrained(base, ADAPTER).eval()
        run("FORMED v0.2.1 (responder disposition)", m, tok, instr)
        del m, base
        if dev == "cuda": torch.cuda.empty_cache()
    if which in ("analyzer", "both"):
        from peft import PeftModel
        base = AutoModelForCausalLM.from_pretrained(BASE, torch_dtype=dt, local_files_only=True).to(dev).eval()
        m = PeftModel.from_pretrained(base, ANALYZER).eval()
        run("ANALYZER v0.1.0 (purpose-formed observer)", m, tok, instr)


if __name__ == "__main__":
    main()
