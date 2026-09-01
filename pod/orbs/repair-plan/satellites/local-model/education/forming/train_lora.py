#!/usr/bin/env python3
# Form the ProntoPC Custodian disposition — a REVERSIBLE LoRA adapter over a FROZEN base.
#
# canon-is-formed-disposition: the disposition becomes intrinsic to the model; the base
# weights are never modified (only a small, hashable, discardable adapter is written). This
# forms toward the residual foci observed on the base (TOTEM-C-0083/-0084): session presence,
# naming-the-injection, grounding/no-fabrication — plus anti-regression "keep" examples.
#
# Trains in the EXACT Gemma chat template the shipping app uses (OnnxGenAiInference.Chat /
# CustodianChat): "<bos><start_of_turn>user\n{u}<end_of_turn>\n<start_of_turn>model\n{a}
# <end_of_turn>\n", masking the prompt so loss is only on the assistant completion.
#
# CPU-only (torch is +cpu here); fp32; base frozen. No gate, no deploy — this only writes an
# adapter + a train log under forming/adapters/. Reversible: delete the adapter to undo.

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
BASE_DIR = os.path.join(ROOT, ".hf-cache", "models--google--gemma-3-1b-it",
                        "snapshots", "dcc83ea841ab6100d6b47a070329e1ba4cf78752")
if not os.path.isdir(BASE_DIR):
    raise FileNotFoundError("base snapshot not found: " + BASE_DIR)
DATA = os.path.join(HERE, "dataset", "formation-seed-v0.1.0.jsonl")
OUT = os.path.join(HERE, "adapters", "gemma3-1b-formation-v0.1.0")

# Hyperparameters — pinned; mirrored in manifest.json.
SEED = 4083
EPOCHS = 6
GRAD_ACCUM = 4
LR = 2e-4
MAX_SEQ = 384
LORA_R = 16
LORA_ALPHA = 32
LORA_DROPOUT = 0.05
TARGETS = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]


def render(user, assistant, tok):
    prompt = f"<bos><start_of_turn>user\n{user}<end_of_turn>\n<start_of_turn>model\n"
    full = prompt + f"{assistant}<end_of_turn>\n"
    p_ids = tok.encode(prompt, add_special_tokens=False)
    f_ids = tok.encode(full, add_special_tokens=False)
    f_ids = f_ids[:MAX_SEQ]
    labels = list(f_ids)
    for i in range(min(len(p_ids), len(labels))):
        labels[i] = -100  # mask the prompt; train only on the assistant completion
    return f_ids, labels


def main():
    torch.manual_seed(SEED)
    t0 = time.time()
    print(f"[forming] base={BASE_DIR}")
    print(f"[forming] loading tokenizer + base (fp32, CPU, frozen)…", flush=True)
    tok = AutoTokenizer.from_pretrained(BASE_DIR, local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(BASE_DIR, torch_dtype=torch.float32, local_files_only=True)
    model.config.use_cache = False

    lora = LoraConfig(r=LORA_R, lora_alpha=LORA_ALPHA, lora_dropout=LORA_DROPOUT,
                      target_modules=TARGETS, bias="none", task_type="CAUSAL_LM")
    model = get_peft_model(model, lora)
    model.print_trainable_parameters()
    model.train()

    rows = [json.loads(l) for l in open(DATA, encoding="utf-8") if l.strip()]
    data = []
    for r in rows:
        u = r["messages"][0]["content"]
        a = r["messages"][1]["content"]
        ids, labels = render(u, a, tok)
        data.append((ids, labels))
    print(f"[forming] examples={len(data)} epochs={EPOCHS} accum={GRAD_ACCUM} lr={LR} seed={SEED}", flush=True)

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

    opt = torch.optim.AdamW([p for p in model.parameters() if p.requires_grad], lr=LR)
    total_steps = (len(loader) * EPOCHS) // GRAD_ACCUM
    warmup = max(1, int(0.1 * total_steps))
    sched = torch.optim.lr_scheduler.LambdaLR(
        opt, lambda s: min(1.0, s / warmup) * (0.5 * (1 + torch.cos(torch.tensor(
            3.14159 * max(0, s - warmup) / max(1, total_steps - warmup))).item())))

    log = {"seed": SEED, "epochs": EPOCHS, "grad_accum": GRAD_ACCUM, "lr": LR,
           "max_seq": MAX_SEQ, "lora_r": LORA_R, "lora_alpha": LORA_ALPHA,
           "target_modules": TARGETS, "examples": len(data), "loss": []}
    step = 0
    for epoch in range(EPOCHS):
        running = 0.0
        n = 0
        opt.zero_grad()
        for i, (input_ids, labels, attn) in enumerate(loader):
            out = model(input_ids=input_ids, attention_mask=attn, labels=labels)
            loss = out.loss / GRAD_ACCUM
            loss.backward()
            running += out.loss.item()
            n += 1
            if (i + 1) % GRAD_ACCUM == 0:
                torch.nn.utils.clip_grad_norm_([p for p in model.parameters() if p.requires_grad], 1.0)
                opt.step(); sched.step(); opt.zero_grad(); step += 1
        avg = running / max(1, n)
        log["loss"].append({"epoch": epoch + 1, "avg_loss": round(avg, 4)})
        print(f"[forming] epoch {epoch+1}/{EPOCHS}  avg_loss={avg:.4f}  elapsed={time.time()-t0:.0f}s", flush=True)

    os.makedirs(OUT, exist_ok=True)
    model.save_pretrained(OUT)
    tok.save_pretrained(OUT)
    log["elapsed_sec"] = round(time.time() - t0, 1)
    log["adapter_dir"] = OUT
    with open(os.path.join(OUT, "train_log.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, indent=2)
    print(f"[forming] DONE — adapter saved to {OUT} in {log['elapsed_sec']}s", flush=True)


if __name__ == "__main__":
    main()
