#!/usr/bin/env python3
# EDUCATE the ProntoPC Custodian disposition — GENERIC (version-parameterized) — a REVERSIBLE
# LoRA over a FROZEN base. Supersedes the v0.2.0-specific educate script as the reusable tool so
# each new version doesn't clone the loop. Same recipe/hyperparameters as educate_lora_v0.2.0.py
# (fp16 AMP + grad-checkpointing on GPU / fp32 CPU fallback; base FROZEN; LoRA fp32; seed 4083).
#
#   python educate_lora.py --version v0.2.1
#     -> reads dataset/formation-seed-v0.2.1.jsonl
#     -> writes adapters/gemma3-1b-formation-v0.2.1/ (adapter + tokenizer + train_log + manifest)
#
# Run in the isolated .venv-gpu (torch 2.6.0+cu124) for GPU. canon-is-formed-disposition: base
# weights never modified. No gate, nothing deployed. Reversible: delete the adapter dir to undo.

import argparse
import hashlib
import json
import os
import time

import torch
from torch.utils.data import DataLoader
from transformers import AutoModelForCausalLM, AutoTokenizer
from peft import LoraConfig, get_peft_model

HERE = os.path.dirname(os.path.abspath(__file__))


def _repo_root(start):
    d = start
    while d != os.path.dirname(d):
        if os.path.isdir(os.path.join(d, ".hf-cache")):
            return d
        d = os.path.dirname(d)
    raise FileNotFoundError("repo root with .hf-cache not found above " + start)


ROOT = _repo_root(HERE)
BASE_SNAPSHOT = "dcc83ea841ab6100d6b47a070329e1ba4cf78752"
BASE_DIR = os.path.join(ROOT, ".hf-cache", "models--google--gemma-3-1b-it", "snapshots", BASE_SNAPSHOT)

# Pinned hyperparameters (identical to v0.2.0, for comparability).
SEED, EPOCHS, GRAD_ACCUM, LR, MAX_SEQ = 4083, 6, 4, 2e-4, 512
LORA_R, LORA_ALPHA, LORA_DROPOUT = 16, 32, 0.05
TARGETS = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def render(user, assistant, tok):
    prompt = f"<bos><start_of_turn>user\n{user}<end_of_turn>\n<start_of_turn>model\n"
    full = prompt + f"{assistant}<end_of_turn>\n"
    p_ids = tok.encode(prompt, add_special_tokens=False)
    f_ids = tok.encode(full, add_special_tokens=False)[:MAX_SEQ]
    labels = list(f_ids)
    for i in range(min(len(p_ids), len(labels))):
        labels[i] = -100
    return f_ids, labels


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", required=True, help="e.g. v0.2.1 -> dataset formation-seed-v0.2.1.jsonl")
    args = ap.parse_args()
    ver = args.version
    DATA = os.path.join(HERE, "dataset", f"formation-seed-{ver}.jsonl")
    OUT = os.path.join(HERE, "adapters", f"gemma3-1b-formation-{ver}")
    if not os.path.isfile(DATA):
        raise FileNotFoundError("dataset not found: " + DATA)
    if not os.path.isdir(BASE_DIR):
        raise FileNotFoundError("base snapshot not found: " + BASE_DIR)

    torch.manual_seed(SEED)
    use_cuda = torch.cuda.is_available()
    device = "cuda" if use_cuda else "cpu"
    dtype = torch.float16 if use_cuda else torch.float32
    if use_cuda:
        torch.cuda.manual_seed_all(SEED)
        torch.cuda.reset_peak_memory_stats()

    t0 = time.time()
    ds_sha = sha256_file(DATA)
    print(f"[educate {ver}] device={device} dtype={dtype}")
    print(f"[educate {ver}] dataset={DATA}\n[educate {ver}] dataset_sha256={ds_sha}", flush=True)

    tok = AutoTokenizer.from_pretrained(BASE_DIR, local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(BASE_DIR, torch_dtype=dtype, local_files_only=True)
    model.config.use_cache = False
    model.to(device)

    lora = LoraConfig(r=LORA_R, lora_alpha=LORA_ALPHA, lora_dropout=LORA_DROPOUT,
                      target_modules=TARGETS, bias="none", task_type="CAUSAL_LM")
    model = get_peft_model(model, lora)
    if use_cuda:
        for p in model.parameters():
            if p.requires_grad:
                p.data = p.data.float()
        model.gradient_checkpointing_enable(gradient_checkpointing_kwargs={"use_reentrant": False})
        model.enable_input_require_grads()
    model.print_trainable_parameters()
    model.train()

    rows = [json.loads(l) for l in open(DATA, encoding="utf-8") if l.strip()]
    data = []
    for r in rows:
        ids, labels = render(r["messages"][0]["content"], r["messages"][1]["content"], tok)
        data.append((ids, labels))
    print(f"[educate {ver}] examples={len(data)} epochs={EPOCHS} accum={GRAD_ACCUM} lr={LR} "
          f"max_seq={MAX_SEQ} seed={SEED}", flush=True)

    pad_id = tok.pad_token_id if tok.pad_token_id is not None else 0

    def collate(batch):
        m = max(len(x[0]) for x in batch)
        ii, ll, aa = [], [], []
        for ids, lab in batch:
            pad = m - len(ids)
            ii.append(ids + [pad_id] * pad); ll.append(lab + [-100] * pad); aa.append([1] * len(ids) + [0] * pad)
        return (torch.tensor(ii), torch.tensor(ll), torch.tensor(aa))

    g = torch.Generator().manual_seed(SEED)
    loader = DataLoader(data, batch_size=1, shuffle=True, collate_fn=collate, generator=g)
    trainable = [p for p in model.parameters() if p.requires_grad]
    opt = torch.optim.AdamW(trainable, lr=LR)
    total_steps = (len(loader) * EPOCHS) // GRAD_ACCUM
    warmup = max(1, int(0.1 * total_steps))
    sched = torch.optim.lr_scheduler.LambdaLR(
        opt, lambda s: min(1.0, s / warmup) * (0.5 * (1 + torch.cos(torch.tensor(
            3.14159 * max(0, s - warmup) / max(1, total_steps - warmup))).item())))
    scaler = torch.cuda.amp.GradScaler(enabled=use_cuda)

    log = {"version": ver, "seed": SEED, "epochs": EPOCHS, "grad_accum": GRAD_ACCUM, "lr": LR,
           "max_seq": MAX_SEQ, "lora_r": LORA_R, "lora_alpha": LORA_ALPHA, "target_modules": TARGETS,
           "examples": len(data), "device": device, "dtype": str(dtype), "loss": []}
    for epoch in range(EPOCHS):
        running, n = 0.0, 0
        opt.zero_grad()
        for i, (input_ids, labels, attn) in enumerate(loader):
            input_ids, labels, attn = input_ids.to(device), labels.to(device), attn.to(device)
            with torch.autocast(device_type="cuda", dtype=torch.float16, enabled=use_cuda):
                out = model(input_ids=input_ids, attention_mask=attn, labels=labels)
                loss = out.loss / GRAD_ACCUM
            scaler.scale(loss).backward()
            running += out.loss.item(); n += 1
            if (i + 1) % GRAD_ACCUM == 0:
                scaler.unscale_(opt)
                torch.nn.utils.clip_grad_norm_(trainable, 1.0)
                scaler.step(opt); scaler.update(); sched.step(); opt.zero_grad()
        avg = running / max(1, n)
        log["loss"].append({"epoch": epoch + 1, "avg_loss": round(avg, 4)})
        peak = (torch.cuda.max_memory_allocated() / 1e9) if use_cuda else 0.0
        print(f"[educate {ver}] epoch {epoch+1}/{EPOCHS}  avg_loss={avg:.4f}  "
              f"elapsed={time.time()-t0:.0f}s  peak_vram={peak:.2f}GB", flush=True)

    os.makedirs(OUT, exist_ok=True)
    model.save_pretrained(OUT); tok.save_pretrained(OUT)
    log["elapsed_sec"] = round(time.time() - t0, 1)
    if use_cuda:
        log["peak_vram_gb"] = round(torch.cuda.max_memory_allocated() / 1e9, 3)
    with open(os.path.join(OUT, "train_log.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, indent=2)
    adapter_file = os.path.join(OUT, "adapter_model.safetensors")
    manifest = {
        "adapter_version": ver, "created_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "base_model": "google/gemma-3-1b-it", "base_snapshot": BASE_SNAPSHOT,
        "dataset_file": os.path.basename(DATA), "dataset_sha256": ds_sha,
        "adapter_sha256": sha256_file(adapter_file) if os.path.isfile(adapter_file) else None,
        "hyperparameters": {"seed": SEED, "epochs": EPOCHS, "grad_accum": GRAD_ACCUM, "lr": LR,
                            "max_seq": MAX_SEQ, "lora_r": LORA_R, "lora_alpha": LORA_ALPHA,
                            "lora_dropout": LORA_DROPOUT, "target_modules": TARGETS},
        "precision": "fp16-amp" if use_cuda else "fp32-cpu",
        "device": (torch.cuda.get_device_name(0) if use_cuda else "cpu"),
        "torch_version": torch.__version__, "elapsed_sec": log["elapsed_sec"],
        "peak_vram_gb": log.get("peak_vram_gb"), "reversible": True,
        "note": "Reversible LoRA over a frozen base (canon-is-formed-disposition). No gate opened; "
                "nothing deployed. Delete this dir to undo.",
    }
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print(f"[educate {ver}] DONE — {OUT} in {log['elapsed_sec']}s; adapter_sha256={manifest['adapter_sha256']}")


if __name__ == "__main__":
    main()
