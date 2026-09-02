#!/usr/bin/env python3
# Merge the purpose-formed analyzer LoRA into the frozen base and export to the shipped int4 ONNX
# GenAI format, so the app's OnnxGenAiInference can run the analyzer on-device (with bounded
# decoding via SetGuidance json_schema). Output -> StoreApp/Assets/models/gemma-3-1b-analyzer/
# (the whole Assets/models/ tree is gitignored; the 700MB weights never enter git). Run in .venv-gpu.

import argparse, os, sys, shutil, subprocess
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
    # The IRG read is an integrity observer — precision matters. Default int4 softened the
    # fp16-validated read (missed fractures, dropped low virtue scores); fp16/bf16 GenAI models do
    # NOT run on the CPU execution provider (the app runs on CPU). The fix that stays CPU-native +
    # ~700MB is a HIGH-ACCURACY int4: accuracy_level=4 (fp32 matmul accumulation) + a finer
    # block_size sharpen the quantized read back toward the fp16 ground truth.
    ap = argparse.ArgumentParser()
    ap.add_argument("--precision", default="int4", choices=["int4", "int8", "fp16", "fp32", "bf16"])
    ap.add_argument("--accuracy-level", default="4", help="int4 only: 0-4 (4 = fp32 accumulation, sharpest)")
    ap.add_argument("--block-size", default="16", help="int4 only: 16/32/64/... (smaller = finer/more accurate)")
    args = ap.parse_args()
    precision = args.precision
    print(f"[export] precision={precision} accuracy_level={args.accuracy_level} block_size={args.block_size}", flush=True)
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

    if os.path.isdir(OUT):
        shutil.rmtree(OUT, ignore_errors=True)  # rebuild clean at the requested precision
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(CACHE, exist_ok=True)
    print(f"[build] onnxruntime-genai builder -> {precision} ONNX (cpu) ...", flush=True)
    cmd = [sys.executable, "-m", "onnxruntime_genai.models.builder",
           "-i", MERGED, "-o", OUT, "-p", precision, "-e", "cpu", "-c", CACHE]
    if precision in ("int4", "int8"):
        # MatMulNBits weight-only quantization options apply to both int4 and int8.
        cmd += ["--extra_options",
                f"accuracy_level={args.accuracy_level}", f"block_size={args.block_size}"]
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
