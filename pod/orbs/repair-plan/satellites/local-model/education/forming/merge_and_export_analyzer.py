#!/usr/bin/env python3
# Merge the purpose-formed analyzer LoRA into the frozen base and export to the shipped int4 ONNX
# GenAI format, so the app's OnnxGenAiInference can run the analyzer on-device (with bounded
# decoding via SetGuidance json_schema). Output -> StoreApp/Assets/models/gemma-3-1b-analyzer/
# (the whole Assets/models/ tree is gitignored; the 700MB weights never enter git). Run in .venv-gpu.

import os, sys, shutil, subprocess
import torch
from transformers import AutoModelForCausalLM, AutoTokenizer
from peft import PeftModel

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import screen_probe as sp  # BASE, ANALYZER, ROOT

MERGED = os.path.join(HERE, "_merged-analyzer")
CACHE = os.path.join(HERE, "_ortbuild-cache")
OUT = os.path.join(sp.ROOT, "StoreApp", "Assets", "models", "gemma-3-1b-analyzer")


def main():
    print("[merge] loading base + analyzer adapter (fp32, CPU)...", flush=True)
    base = AutoModelForCausalLM.from_pretrained(sp.BASE, torch_dtype=torch.float32, local_files_only=True)
    model = PeftModel.from_pretrained(base, sp.ANALYZER)
    print("[merge] merging LoRA into the base weights (merge_and_unload)...", flush=True)
    merged = model.merge_and_unload().half()
    if os.path.isdir(MERGED):
        shutil.rmtree(MERGED, ignore_errors=True)
    os.makedirs(MERGED, exist_ok=True)
    merged.save_pretrained(MERGED)
    AutoTokenizer.from_pretrained(sp.BASE, local_files_only=True).save_pretrained(MERGED)
    print("[merge] merged HF model saved ->", MERGED, flush=True)

    os.makedirs(OUT, exist_ok=True)
    os.makedirs(CACHE, exist_ok=True)
    print("[build] onnxruntime-genai builder -> int4 ONNX (cpu) ...", flush=True)
    cmd = [sys.executable, "-m", "onnxruntime_genai.models.builder",
           "-i", MERGED, "-o", OUT, "-p", "int4", "-e", "cpu", "-c", CACHE]
    print("[build]", " ".join(cmd), flush=True)
    r = subprocess.run(cmd)
    if r.returncode != 0:
        print("[build] FAILED rc", r.returncode, flush=True)
        sys.exit(r.returncode)
    shutil.rmtree(MERGED, ignore_errors=True)
    print("[build] DONE ->", OUT, flush=True)
    print("[build] contents:", os.listdir(OUT), flush=True)


if __name__ == "__main__":
    main()
