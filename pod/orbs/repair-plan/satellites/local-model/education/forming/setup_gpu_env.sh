#!/usr/bin/env bash
# GPU education-session env setup (isolated venv; does NOT touch the proven CPU env).
# Target: GTX 1060 6GB (Pascal sm_61). Installs a CUDA torch + the forming stack,
# then runs a real GPU sanity check. Everything logged; failure falls back to CPU.
set -o pipefail
EDU="H:/PRONTOPC_PRODUCTION/pod/orbs/repair-plan/satellites/local-model/education/forming"
VENV="$EDU/.venv-gpu"
LOG="$EDU/setup-gpu.log"
PY="$VENV/Scripts/python.exe"

{
  echo "=== GPU env setup start $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
  echo "[1/5] creating isolated venv at $VENV"
  python -m venv "$VENV" || { echo "FAIL: venv creation"; exit 1; }
  "$PY" -m pip install --upgrade pip setuptools wheel 2>&1 | tail -3

  echo "[2/5] installing CUDA torch (cu124 index; supports Pascal sm_61 via PTX)"
  "$PY" -m pip install torch --index-url https://download.pytorch.org/whl/cu124 2>&1 | tail -8
  # If cu124 has no compatible wheel, try cu126 as a fallback.
  if ! "$PY" -c "import torch,sys; sys.exit(0 if torch.version.cuda else 1)" 2>/dev/null; then
    echo "  cu124 gave a non-CUDA torch; retrying cu126"
    "$PY" -m pip install --force-reinstall torch --index-url https://download.pytorch.org/whl/cu126 2>&1 | tail -8
  fi

  echo "[3/5] installing forming stack (pinned to the proven versions)"
  "$PY" -m pip install "transformers==4.57.1" "peft==0.20.0" accelerate datasets safetensors sentencepiece 2>&1 | tail -6

  echo "[4/5] versions"
  "$PY" - <<'PYEOF'
import torch
print("torch", torch.__version__, "| bundled CUDA", torch.version.cuda)
print("cuda available:", torch.cuda.is_available())
try:
    import transformers, peft
    print("transformers", transformers.__version__, "| peft", peft.__version__)
except Exception as e:
    print("stack import warn:", e)
PYEOF

  echo "[5/5] REAL GPU sanity check (device, capability, matmul on GPU)"
  "$PY" - <<'PYEOF'
import torch
if not torch.cuda.is_available():
    print("RESULT: NO_CUDA  (torch cannot see the GPU) -> education runs on CPU")
    raise SystemExit(0)
name = torch.cuda.get_device_name(0)
cap = torch.cuda.get_device_capability(0)
free, total = torch.cuda.mem_get_info()
print(f"device: {name}  capability sm_{cap[0]}{cap[1]}  vram_free={free/1e9:.2f}GB/{total/1e9:.2f}GB")
try:
    a = torch.randn(2048, 2048, device="cuda", dtype=torch.float16)
    b = torch.randn(2048, 2048, device="cuda", dtype=torch.float16)
    c = (a @ b).float().sum().item()
    torch.cuda.synchronize()
    print(f"RESULT: GPU_OK  fp16 matmul on {name} succeeded (checksum {c:.1f})")
except Exception as e:
    print(f"RESULT: GPU_MATMUL_FAIL  {type(e).__name__}: {e}  -> education runs on CPU")
PYEOF

  echo "=== GPU env setup done $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
} > "$LOG" 2>&1
