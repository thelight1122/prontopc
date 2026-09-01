#!/usr/bin/env python3
# EDUCATE the ProntoPC Custodian disposition — v0.2.0 — a REVERSIBLE LoRA over a FROZEN base.
#
# "Educate, not train" (AEGIS vocabulary): this forms a disposition into a small, hashable,
# discardable adapter; the base weights are NEVER modified (canon-is-formed-disposition).
# v0.2.0 forms toward the v0.1.0 foci PLUS the Canon operating-parameters (formed as
# disposition) and the Tech Library repair knowledge — dataset formation-seed-v0.2.0.jsonl.
#
# GPU path for a 6 GB GTX 1060 (Pascal sm_61, no bf16): base loaded fp16 (~2 GB, frozen),
# LoRA params upcast to fp32 for stable optimization, fp16 AMP (GradScaler) for the forward,
# gradient checkpointing to keep activation memory low. Runs in the isolated .venv-gpu
# (torch 2.6.0+cu124) — invoke with .venv-gpu/Scripts/python.exe. Falls back to CPU fp32 if
# CUDA is unavailable, so the run is never blocked.
#
# EXACT Gemma chat template the shipping app uses (OnnxGenAiInference.Chat / CustodianChat):
#   "<bos><start_of_turn>user\n{u}<end_of_turn>\n<start_of_turn>model\n{a}<end_of_turn>\n"
# prompt masked so loss is only on the assistant completion — identical to v0.1.0.
#
# Writes: adapters/gemma3-1b-formation-v0.2.0/ (adapter + tokenizer) + train_log.json +
# manifest.json (pins base snapshot / dataset sha256 / adapter sha256 / hyperparams / device).
# NO gate opened, NOTHING deployed. Reversible: delete the adapter dir to undo.

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
BASE_DIR = os.path.join(ROOT, ".hf-cache", "models--google--gemma-3-1b-it",
                        "snapshots", BASE_SNAPSHOT)
if not os.path.isdir(BASE_DIR):
    raise FileNotFoundError("base snapshot not found: " + BASE_DIR)
DATA = os.path.join(HERE, "dataset", "formation-seed-v0.2.0.jsonl")
OUT = os.path.join(HERE, "adapters", "gemma3-1b-formation-v0.2.0")
REVIEWED_DATASET_SHA = "826c0824dc6d70a6d66df10800546a8bdd0fb03aee5a825e41e83dc11b1aec2f"

# Hyperparameters — pinned; recorded in manifest.json. Parity with v0.1.0 except MAX_SEQ (bumped
# to 512 for the longer Canon/Tech answers) and precision (fp16 AMP on GPU vs CPU fp32).
SEED = 4083
EPOCHS = 6
GRAD_ACCUM = 4
LR = 2e-4
MAX_SEQ = 512
LORA_R = 16
LORA_ALPHA = 32
LORA_DROPOUT = 0.05
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
        labels[i] = -100  # mask the prompt; educate only on the assistant completion
    return f_ids, labels


def main():
    torch.manual_seed(SEED)
    use_cuda = torch.cuda.is_available()
    device = "cuda" if use_cuda else "cpu"
    dtype = torch.float16 if use_cuda else torch.float32
    if use_cuda:
        torch.cuda.manual_seed_all(SEED)
        torch.cuda.reset_peak_memory_stats()

    t0 = time.time()
    ds_sha = sha256_file(DATA)
    print(f"[educate] device={device} dtype={dtype}")
    print(f"[educate] base={BASE_DIR}")
    print(f"[educate] dataset={DATA}\n[educate] dataset_sha256={ds_sha} "
          f"{'== reviewed' if ds_sha == REVIEWED_DATASET_SHA else '!! DIFFERS from reviewed'}",
          flush=True)

    tok = AutoTokenizer.from_pretrained(BASE_DIR, local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(
        BASE_DIR, torch_dtype=dtype, local_files_only=True)
    model.config.use_cache = False
    model.to(device)

    lora = LoraConfig(r=LORA_R, lora_alpha=LORA_ALPHA, lora_dropout=LORA_DROPOUT,
                      target_modules=TARGETS, bias="none", task_type="CAUSAL_LM")
    model = get_peft_model(model, lora)

    # Frozen fp16 base + fp32 LoRA master weights = stable AMP optimization on Pascal.
    if use_cuda:
        for p in model.parameters():
            if p.requires_grad:
                p.data = p.data.float()
        model.gradient_checkpointing_enable(gradient_checkpointing_kwargs={"use_reentrant": False})
        model.enable_input_require_grads()  # needed so checkpointing backprops to LoRA
    model.print_trainable_parameters()
    model.train()

    rows = [json.loads(l) for l in open(DATA, encoding="utf-8") if l.strip()]
    data = []
    for r in rows:
        u = r["messages"][0]["content"]
        a = r["messages"][1]["content"]
        ids, labels = render(u, a, tok)
        data.append((ids, labels))
    print(f"[educate] examples={len(data)} epochs={EPOCHS} accum={GRAD_ACCUM} "
          f"lr={LR} max_seq={MAX_SEQ} seed={SEED}", flush=True)

    pad_id = tok.pad_token_id if tok.pad_token_id is not None else 0

    def collate(batch):
        m = max(len(x[0]) for x in batch)
        input_ids, labels, attn = [], [], []
        for ids, lab in batch:
            pad = m - len(ids)
            input_ids.append(ids + [pad_id] * pad)
            labels.append(lab + [-100] * pad)
            attn.append([1] * len(ids) + [0] * pad)
        return (torch.tensor(input_ids), torch.tensor(labels), torch.tensor(attn))

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

    log = {"seed": SEED, "epochs": EPOCHS, "grad_accum": GRAD_ACCUM, "lr": LR,
           "max_seq": MAX_SEQ, "lora_r": LORA_R, "lora_alpha": LORA_ALPHA,
           "target_modules": TARGETS, "examples": len(data), "device": device,
           "dtype": str(dtype), "loss": []}
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
        print(f"[educate] epoch {epoch+1}/{EPOCHS}  avg_loss={avg:.4f}  "
              f"elapsed={time.time()-t0:.0f}s  peak_vram={peak:.2f}GB", flush=True)

    os.makedirs(OUT, exist_ok=True)
    model.save_pretrained(OUT)
    tok.save_pretrained(OUT)
    log["elapsed_sec"] = round(time.time() - t0, 1)
    if use_cuda:
        log["peak_vram_gb"] = round(torch.cuda.max_memory_allocated() / 1e9, 3)
    with open(os.path.join(OUT, "train_log.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, indent=2)

    adapter_file = os.path.join(OUT, "adapter_model.safetensors")
    manifest = {
        "adapter_version": "v0.2.0",
        "created_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "base_model": "google/gemma-3-1b-it",
        "base_snapshot": BASE_SNAPSHOT,
        "dataset_file": os.path.basename(DATA),
        "dataset_sha256": ds_sha,
        "dataset_matches_reviewed": ds_sha == REVIEWED_DATASET_SHA,
        "adapter_sha256": sha256_file(adapter_file) if os.path.isfile(adapter_file) else None,
        "hyperparameters": {
            "seed": SEED, "epochs": EPOCHS, "grad_accum": GRAD_ACCUM, "lr": LR,
            "max_seq": MAX_SEQ, "lora_r": LORA_R, "lora_alpha": LORA_ALPHA,
            "lora_dropout": LORA_DROPOUT, "target_modules": TARGETS,
        },
        "precision": "fp16-amp" if use_cuda else "fp32-cpu",
        "device": (torch.cuda.get_device_name(0) if use_cuda else "cpu"),
        "torch_version": torch.__version__,
        "elapsed_sec": log["elapsed_sec"],
        "peak_vram_gb": log.get("peak_vram_gb"),
        "reversible": True,
        "note": "Reversible LoRA over a frozen base (canon-is-formed-disposition). "
                "No gate opened; nothing deployed. Delete this dir to undo.",
    }
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print(f"[educate] DONE — adapter+manifest saved to {OUT} in {log['elapsed_sec']}s")
    print(f"[educate] adapter_sha256={manifest['adapter_sha256']}")


if __name__ == "__main__":
    main()
