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
    ap.add_argument("--base-dir", default=sp.BASE, help="HF snapshot of the base (default: the 1B)")
    ap.add_argument("--adapter", default=sp.ANALYZER, help="LoRA adapter dir to merge")
    ap.add_argument("--out-name", default="gemma-3-1b-analyzer", help="Assets/models/<out-name> ONNX dir")
    ap.add_argument("--merge-dtype", default="fp32", choices=["fp32", "fp16"],
                    help="precision the LoRA is merged in. fp32 = the PROVEN-SHARP path for the 1B "
                         "analyzer (TOTEM-C-0095/-0096): the small fracture delta is applied precisely, "
                         "THEN .half()'d, THEN int4-quantized. fp16 = RAM-safe for a bigger base, but it "
                         "rounds the base AND the delta to fp16 before the merge, blunting the fracture "
                         "signal ahead of int4 (TOTEM-C-0098 finding: fp16-merge collapsed the on-device "
                         "read to all-100). Keep fp32 for the 1B; use fp16 only when the base won't fit fp32.")
    args = ap.parse_args()
    precision = args.precision
    merge_dtype = torch.float32 if args.merge_dtype == "fp32" else torch.float16
    OUT = os.path.join(sp.ROOT, "StoreApp", "Assets", "models", args.out_name)
    print(f"[export] precision={precision} accuracy_level={args.accuracy_level} block_size={args.block_size} "
          f"merge_dtype={args.merge_dtype} base={args.base_dir} adapter={args.adapter} out={args.out_name}", flush=True)
    print(f"[merge] loading base + adapter ({args.merge_dtype}, CPU)...", flush=True)
    base = AutoModelForCausalLM.from_pretrained(args.base_dir, torch_dtype=merge_dtype, local_files_only=True)
    model = PeftModel.from_pretrained(base, args.adapter)
    print("[merge] merging LoRA into the base weights (merge_and_unload)...", flush=True)
    merged = model.merge_and_unload()
    if merge_dtype == torch.float32:
        merged = merged.half()  # fp32-precise merge, then fp16 on disk for the int4 builder (v0.2.0 path)
    if os.path.isdir(MERGED):
        shutil.rmtree(MERGED, ignore_errors=True)
    os.makedirs(MERGED, exist_ok=True)
    merged.save_pretrained(MERGED)
    AutoTokenizer.from_pretrained(args.base_dir, local_files_only=True).save_pretrained(MERGED)
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
