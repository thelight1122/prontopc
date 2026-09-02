#!/usr/bin/env python3
# EDUCATE a disposition on a BIGGER base via QLoRA (4-bit frozen base + LoRA) so it fits the 6 GB
# GTX 1060 — the path to a model with the capacity the 1B lacked (e.g. holding the Loyalty/Trust
# consent-violation association through int4; TOTEM-C-0098). Reuses educate_lora's render + masking
# + hyperparameters verbatim (single source of truth); only the model LOADING differs (4-bit).
#
#   python educate_qlora.py --version analyzer-v0.3.0 --base-dir <gemma-3-4b snapshot> \
#          --base-name google/gemma-3-4b-it --out-prefix gemma3-4b-formation --max-seq 1024
#
# Writes adapters/<out-prefix>-<version>/ + train_log + manifest. Base weights never modified
# (reversible LoRA over a frozen 4-bit base). No gate, nothing deployed. Run in .venv-gpu.

import argparse, json, os, time
import torch
from torch.utils.data import DataLoader
from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
from peft import LoraConfig, get_peft_model, prepare_model_for_kbit_training

HERE = os.path.dirname(os.path.abspath(__file__))
import sys
sys.path.insert(0, HERE)
import educate_lora as el  # render, sha256_file, SEED, EPOCHS, GRAD_ACCUM, LR, LORA_*, TARGETS


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", required=True)
    ap.add_argument("--base-dir", required=True, help="local HF snapshot dir of the bigger base")
    ap.add_argument("--base-name", required=True, help="e.g. google/gemma-3-4b-it (for the manifest)")
    ap.add_argument("--out-prefix", required=True, help="e.g. gemma3-4b-formation")
    ap.add_argument("--max-seq", type=int, default=1024)
    args = ap.parse_args()
    el.MAX_SEQ = args.max_seq  # educate_lora.render reads this global

    ver = args.version
    DATA = os.path.join(HERE, "dataset", f"formation-seed-{ver}.jsonl")
    OUT = os.path.join(HERE, "adapters", f"{args.out_prefix}-{ver}")
    if not os.path.isfile(DATA):
        raise FileNotFoundError("dataset not found: " + DATA)
    if not os.path.isdir(args.base_dir):
        raise FileNotFoundError("base snapshot not found: " + args.base_dir)
    if not torch.cuda.is_available():
        raise RuntimeError("QLoRA path needs CUDA")

    torch.manual_seed(el.SEED); torch.cuda.manual_seed_all(el.SEED); torch.cuda.reset_peak_memory_stats()
    t0 = time.time()
    ds_sha = el.sha256_file(DATA)
    print(f"[qlora {ver}] base={args.base_name} 4-bit | dataset_sha256={ds_sha}", flush=True)

    tok = AutoTokenizer.from_pretrained(args.base_dir, local_files_only=True)
    bnb = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4",
                             bnb_4bit_compute_dtype=torch.float16, bnb_4bit_use_double_quant=True)
    model = AutoModelForCausalLM.from_pretrained(
        args.base_dir, quantization_config=bnb, device_map={"": 0}, local_files_only=True)
    model.config.use_cache = False
    model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=True)
    lora = LoraConfig(r=el.LORA_R, lora_alpha=el.LORA_ALPHA, lora_dropout=el.LORA_DROPOUT,
                      target_modules=el.TARGETS, bias="none", task_type="CAUSAL_LM")
    model = get_peft_model(model, lora)
    model.print_trainable_parameters()
    model.train()

    rows = [json.loads(l) for l in open(DATA, encoding="utf-8") if l.strip()]
    data = [el.render(r["messages"][0]["content"], r["messages"][1]["content"], tok) for r in rows]
    print(f"[qlora {ver}] examples={len(data)} epochs={el.EPOCHS} accum={el.GRAD_ACCUM} "
          f"lr={el.LR} max_seq={args.max_seq} seed={el.SEED}", flush=True)

    pad_id = tok.pad_token_id if tok.pad_token_id is not None else 0

    def collate(batch):
        m = max(len(x[0]) for x in batch)
        ii, ll, aa = [], [], []
        for ids, lab in batch:
            pad = m - len(ids)
            ii.append(ids + [pad_id] * pad); ll.append(lab + [-100] * pad); aa.append([1] * len(ids) + [0] * pad)
        return (torch.tensor(ii), torch.tensor(ll), torch.tensor(aa))

    g = torch.Generator().manual_seed(el.SEED)
    loader = DataLoader(data, batch_size=1, shuffle=True, collate_fn=collate, generator=g)
    trainable = [p for p in model.parameters() if p.requires_grad]
    opt = torch.optim.AdamW(trainable, lr=el.LR)
    total_steps = (len(loader) * el.EPOCHS) // el.GRAD_ACCUM
    warmup = max(1, int(0.1 * total_steps))
    sched = torch.optim.lr_scheduler.LambdaLR(
        opt, lambda s: min(1.0, s / warmup) * (0.5 * (1 + torch.cos(torch.tensor(
            3.14159 * max(0, s - warmup) / max(1, total_steps - warmup))).item())))
    scaler = torch.cuda.amp.GradScaler()

    log = {"version": ver, "base": args.base_name, "seed": el.SEED, "epochs": el.EPOCHS,
           "grad_accum": el.GRAD_ACCUM, "lr": el.LR, "max_seq": args.max_seq, "lora_r": el.LORA_R,
           "lora_alpha": el.LORA_ALPHA, "target_modules": el.TARGETS, "examples": len(data),
           "precision": "qlora-nf4", "loss": []}
    for epoch in range(el.EPOCHS):
        running, n = 0.0, 0
        opt.zero_grad()
        for i, (input_ids, labels, attn) in enumerate(loader):
            input_ids, labels, attn = input_ids.cuda(), labels.cuda(), attn.cuda()
            with torch.autocast(device_type="cuda", dtype=torch.float16):
                out = model(input_ids=input_ids, attention_mask=attn, labels=labels)
                loss = out.loss / el.GRAD_ACCUM
            scaler.scale(loss).backward()
            running += out.loss.item(); n += 1
            if (i + 1) % el.GRAD_ACCUM == 0:
                scaler.unscale_(opt); torch.nn.utils.clip_grad_norm_(trainable, 1.0)
                scaler.step(opt); scaler.update(); sched.step(); opt.zero_grad()
        avg = running / max(1, n)
        log["loss"].append({"epoch": epoch + 1, "avg_loss": round(avg, 4)})
        print(f"[qlora {ver}] epoch {epoch+1}/{el.EPOCHS}  avg_loss={avg:.4f}  "
              f"elapsed={time.time()-t0:.0f}s  peak_vram={torch.cuda.max_memory_allocated()/1e9:.2f}GB", flush=True)

    os.makedirs(OUT, exist_ok=True)
    model.save_pretrained(OUT); tok.save_pretrained(OUT)
    log["elapsed_sec"] = round(time.time() - t0, 1)
    log["peak_vram_gb"] = round(torch.cuda.max_memory_allocated() / 1e9, 3)
    with open(os.path.join(OUT, "train_log.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, indent=2)
    af = os.path.join(OUT, "adapter_model.safetensors")
    manifest = {"adapter_version": ver, "created_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "base_model": args.base_name, "dataset_file": os.path.basename(DATA), "dataset_sha256": ds_sha,
                "adapter_sha256": el.sha256_file(af) if os.path.isfile(af) else None,
                "precision": "qlora-nf4 (educate) -> int4 ONNX (ship)", "device": torch.cuda.get_device_name(0),
                "torch_version": torch.__version__, "elapsed_sec": log["elapsed_sec"],
                "peak_vram_gb": log["peak_vram_gb"], "reversible": True,
                "note": "Reversible LoRA over a frozen 4-bit base. No gate opened; nothing deployed."}
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print(f"[qlora {ver}] DONE -> {OUT} in {log['elapsed_sec']}s; adapter_sha256={manifest['adapter_sha256']}")


if __name__ == "__main__":
    main()
