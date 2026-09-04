"""
ProntoPC — Microsoft Store certification pre-flight  (v2).

Tests every FUNCTIONAL CLAIM the Store listing makes — and now EXECUTES the
behaviours a machine can, rather than trusting that a source file exists. Exits
non-zero if any blocking claim cannot be demonstrated, or if a blocking gate can
only be satisfied by a manual step that has not been done.

The point of this file: "ready to submit" stops being anyone's judgement and
becomes a test result. Four consecutive certification rejections each fixed one
thing and were then caught by the next unverified claim (see POLICY note below).

Product shape (correcting a common misread — this is NOT a self-contained,
no-network app):
  • The repair PLAN is authored ON-DEVICE by a bundled Gemma model, with NO cloud
    authoring fallback (retired 2026-09-02) — a local decline leads to honest
    make-good, never a cloud- or heuristic-authored plan. So NO network is needed
    for the AI plan itself.
  • BUT the product is PAID: each run is unlocked by a one-time $29 Stripe checkout
    in the browser, the reviewer returns via prontopc.online, and the app talks to
    the Cloud Run backend for payment-verify, consent, the repair report, refunds,
    and make-good. Those web/Stripe/Cloud-Run gates are the SHIPPING reviewer path
    (they catch the 10.3.3 no-cost-access and 10.1.2.10 post-checkout rejections) —
    they are NOT legacy and must stay blocking.

v2 upgrades (executed evidence, not file existence):
  • [3b] ModelSmoke is actually RUN; PASS only on a real on-device plan + exit 0.
  • [6]  The final .msix/.msixupload PACKAGE is inspected directly (model assets +
         version), because the package — not the source tree — is what Microsoft
         receives.
  • [4]  Active listing claims (categories included) are BLOCKING.
  • [7]  Windows App Certification Kit (WACK): run if asked, else an explicit
         blocking manual-required — never a silent pass.
  • A machine-readable evidence report is written every run (--evidence-out).
  • Final status is CERT_READY only when every blocking gate — including unresolved
    manual-required gates — has produced affirmative evidence.

Run:
  python certification_preflight.py
  python certification_preflight.py --package <path-to-.msix-or-.msixupload>
  python certification_preflight.py --no-smoke            # skip the slow ModelSmoke
  python certification_preflight.py --wack                # attempt an automated WACK run (needs elevation)
  python certification_preflight.py --evidence-out docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json
"""

import argparse
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
BACKEND = "https://pronto-pc-108655745293.us-central1.run.app"
SITE = "https://prontopc.online"
CERT_CODE = "MSCERT26"
PAYMENT_LINK = "plink_1Ta4SfLLKFLplB2ZeOVKy2ol"
CLAIMED_CATEGORIES = 35      # "35+ category diagnostic scan"
CLAIMED_REPAIRS = 18         # "18 repair capabilities"
CLAIMED_SESSION_DAYS = 3     # "3-day session window"

PREFLIGHT_VERSION = "2.0"
MODEL_NAME = "gemma-3-1b-it-formed-v0.2.1"
MODEL_DIR = HERE / "StoreApp" / "Assets" / "models" / MODEL_NAME
SOURCE_MANIFEST = HERE / "StoreApp" / "Package.appxmanifest"
DEFAULT_EVIDENCE = HERE / "docs" / "CERTIFICATION_PREFLIGHT_EVIDENCE.json"
WACK_EXE = Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) \
    / "Windows Kits" / "10" / "App Certification Kit" / "appcert.exe"

# Historical Microsoft certification rejection references. These are the concrete
# failures this preflight regresses against — labelled HISTORICAL so nobody mistakes
# an old policy number for a guarantee of the current policy text (POLICY-001).
HISTORICAL_POLICY = [
    ("10.3.3",    "reviewer could not get past the $29 paywall (fixed: 100%-off reviewer code)"),
    ("10.1.2.10", "after $0 checkout, /success failed to reach the backend"),
    ("10.1.2.10", "'repair plan built by AI' absent — both cloud LLMs were dead and the client fell back to a rule-based plan (fixed: on-device authoring, no fallback)"),
]

RESULTS = []


# ── result recording ─────────────────────────────────────────────────────────
# Three outcomes matter: PASS, FAIL (blocking), WARN (non-blocking). A fourth,
# MANUAL, is a blocking gate a machine can't safely automate here — it holds the
# run at DO_NOT_SUBMIT until a human does it (never a silent pass).

def record(claim, ok, evidence, blocking=True):
    state = "pass" if ok else ("fail" if blocking else "warn")
    RESULTS.append({"claim": claim, "ok": bool(ok), "evidence": str(evidence)[:300],
                    "blocking": blocking, "state": state, "manual": False})
    mark = {"pass": "PASS", "fail": "FAIL", "warn": "WARN"}[state]
    print("  [%-4s] %-46s %s" % (mark, claim[:46], str(evidence)[:110]))


def record_manual(claim, note):
    RESULTS.append({"claim": claim, "ok": False, "evidence": str(note)[:300],
                    "blocking": True, "state": "manual", "manual": True})
    print("  [%-4s] %-46s %s" % ("MANL", claim[:46], str(note)[:110]))


# ── Stripe / HTTP helpers (unchanged) ────────────────────────────────────────

def stripe_key():
    """Read the live Stripe key from Cloud Run. Never printed."""
    out = subprocess.run(
        "gcloud run services describe pronto-pc --region us-central1 --format json",
        capture_output=True, text=True, timeout=180, shell=True)
    if not out.stdout.strip():
        return None
    env = json.loads(out.stdout)["spec"]["template"]["spec"]["containers"][0].get("env", [])
    for e in env:
        if e["name"] == "STRIPE_SECRET_KEY":
            return e.get("value")
    return None


def stripe_get(path, key):
    req = urllib.request.Request("https://api.stripe.com/v1/" + path,
                                 headers={"Authorization": "Bearer " + key})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())


def http_status(url, timeout=30):
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "preflight"})
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status
    except urllib.error.HTTPError as e:
        return e.code
    except Exception as e:
        return "ERR:" + str(e)[:60]


def sha256_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


# ── 0. Policy references (historical, not current-policy guarantees) ─────────

def print_policy_note():
    print("\n[0] Historical rejection references (regressed against; NOT current-policy text)")
    for code, desc in HISTORICAL_POLICY:
        print("  [HIST] %-10s %s" % (code, desc[:96]))
    print("  [NOTE] Re-check the CURRENT Microsoft Store policy text at submission time;")
    print("         these codes are why each gate exists, not a claim they are still numbered so.")


# ── 1. Free test access for the reviewer (historical 10.3.3) ─────────────────

def check_reviewer_access(key):
    print("\n[1] Reviewer can reach the product at no cost  (historical 10.3.3)")
    if not key:
        record("Stripe key retrievable", False, "could not read STRIPE_SECRET_KEY")
        return
    try:
        pl = stripe_get("payment_links/" + PAYMENT_LINK, key)
        record("payment link active", pl.get("active"), pl.get("url"))
        record("promo codes enabled on link", pl.get("allow_promotion_codes"),
               "allow_promotion_codes=%s" % pl.get("allow_promotion_codes"))
    except Exception as e:
        record("payment link reachable", False, e)

    try:
        pcs = stripe_get("promotion_codes?code=" + CERT_CODE, key)
        c = (pcs.get("data") or [None])[0]
        if not c:
            record("cert code %s exists" % CERT_CODE, False, "not found")
            return
        record("cert code %s exists" % CERT_CODE, True, c["id"])
        record("cert code active", c.get("active"), "active=%s" % c.get("active"))
        full = stripe_get("promotion_codes/" + c["id"] + "?expand[]=promotion.coupon", key)
        coup = (full.get("promotion") or {}).get("coupon")
        if coup is None:
            coup = full.get("coupon")          # legacy API shape
        if isinstance(coup, str):
            coup = stripe_get("coupons/" + coup, key)
        coup = coup or {}
        pct = coup.get("percent_off")
        record("cert code is 100%% off (no card needed)", pct == 100,
               "coupon=%s percent_off=%s valid=%s" % (coup.get("id"), pct, coup.get("valid")))
        left = (c.get("max_redemptions") or 0) - (c.get("times_redeemed") or 0)
        record("cert code has redemptions left", left > 0, "%s remaining" % left)
        exp = c.get("expires_at")
        record("cert code not expired", (exp is None) or exp > time.time(),
               "expires_at=%s" % exp)
    except Exception as e:
        record("cert code check", False, e)


# ── 2. The post-checkout path the reviewer walks (historical 10.1.2.10) ──────

def check_success_path():
    print("\n[2] Post-checkout path resolves  (historical 10.1.2.10)")
    s = http_status(SITE)
    record("prontopc.online reachable", s == 200, "HTTP %s" % s)
    s = http_status(SITE + "/success")
    record("/success page reachable", s == 200, "HTTP %s" % s)
    s = http_status(BACKEND)
    record("backend responding", isinstance(s, int), "HTTP %s" % s)


# ── 3. THE CLAIM THAT FAILED: an AI-built repair plan (source bundling) ──────
# The plan is authored ON-DEVICE (bundled Gemma) with NO cloud fallback. Section 3
# confirms the model is bundled in the SOURCE tree; section 3b actually RUNS it; and
# section 6 confirms it is inside the real submission PACKAGE.

def check_ai_repair_plan():
    print("\n[3] \"AI-powered repair plan\" model bundled in source  (historical 10.1.2.10)")
    cfg = MODEL_DIR / "genai_config.json"
    weights = MODEL_DIR / "model.onnx.data"
    formation = MODEL_DIR / "aegis-formation-manifest.json"
    nature = HERE / "pod" / "orbs" / "repair-plan" / "satellites" / "local-model" / "education" / "nature-layer" / "repair-nature-layer.json"
    have_model = (cfg.exists() and weights.exists()
                  and weights.stat().st_size > 100_000_000)
    record("on-device model bundled (genai_config + weights)", have_model,
           str(MODEL_DIR) if have_model else "MISSING at %s" % MODEL_DIR)
    record("formed responder carries formation provenance", formation.exists(),
           str(formation) if formation.exists() else "formation manifest missing")
    record("Product Nature Layer is present", nature.exists(),
           str(nature) if nature.exists() else "Nature Layer source missing")


# ── 3b. EXECUTE the on-device authoring (AI-001 / EVIDENCE-001) ──────────────

def run_model_smoke(enabled):
    print("\n[3b] ModelSmoke EXECUTES real on-device authoring  (AI-001)")
    if not enabled:
        record_manual("ModelSmoke executed (real on-device plan)",
                      "skipped via --no-smoke; run: dotnet run --project tests/ModelSmoke -c Release")
        return
    if not (MODEL_DIR / "model.onnx.data").exists():
        record_manual("ModelSmoke executed (real on-device plan)",
                      "the ~700MB model is not in this checkout; run on a tree that has it")
        return
    t0 = time.time()
    try:
        out = subprocess.run("dotnet run --project tests/ModelSmoke -c Release",
                             capture_output=True, text=True, timeout=900, shell=True, cwd=str(HERE))
    except subprocess.TimeoutExpired:
        record("ModelSmoke PASS (on-device plan + admissible action)", False,
               "TIMEOUT after 15 min")
        return
    dur = time.time() - t0
    stdout = out.stdout or ""
    ok = out.returncode == 0 and "MODEL SMOKE PASS" in stdout
    tail = stdout.strip().splitlines()[-1] if stdout.strip() else "(no output)"
    record("ModelSmoke PASS (on-device plan + admissible action)", ok,
           "exit=%s %.0fs — %s" % (out.returncode, dur, tail[:90]))


# ── 4. Countable claims made in the listing (CLAIM-001: all blocking) ────────

def check_countable_claims():
    print("\n[4] Countable listing claims match the code  (all blocking while claimed)")
    src = HERE / "pronto-server-cloudrun.mjs"
    text = src.read_text(encoding="utf-8", errors="replace") if src.exists() else ""

    marker = "Known repair action IDs you may use:"
    ids = set()
    if marker in text:
        block = text.split(marker, 1)[1]
        block = block.strip().split(chr(10) + chr(10))[0]
        ids = {t.strip().split(" ")[0] for t in block.split(",") if t.strip()}
    record("repair capabilities >= %d claimed" % CLAIMED_REPAIRS,
           len(ids) >= CLAIMED_REPAIRS,
           "%d repair action IDs in catalogue" % len(ids))

    m = re.search(r"SESSION_TTL_MS\s*=\s*([0-9]+)\s*\*", text)
    days = int(m.group(1)) if m else None
    record("session window is %d days as claimed" % CLAIMED_SESSION_DAYS,
           days == CLAIMED_SESSION_DAYS, "SESSION_TTL_MS starts at %s" % days)

    # Diagnostic categories — BLOCKING (CLAIM-001): the "35+ categories" listing
    # claim can cause rejection, so it may not be a soft warning.
    diag = HERE / "StoreApp" / "Diagnostics.cs"
    if diag.exists():
        d = diag.read_text(encoding="utf-8", errors="replace")
        cats = set(re.findall(r'\["([a-z_]+)"\]\s*=', d))
        record("diagnostic categories >= %d claimed" % CLAIMED_CATEGORIES,
               len(cats) >= CLAIMED_CATEGORIES,
               "%d categories found" % len(cats))
    else:
        record("Diagnostics.cs present", False, "not found")


# ── 5. Certification notes match the actual reviewer flow ─────────────────────
# ARCH-001 correction: the shipping app is NOT self-contained-with-no-website. It
# is a paid product — the reviewer opens a $29 Stripe checkout (100%-off with the
# cert code), returns via prontopc.online, and the app uses the Cloud Run backend.
# What IS on-device is the AI PLAN (no network needed to author it). The notes must
# describe that true flow, so these checks confirm the on-device-AI + reviewer flow
# language is present — not that the app has no website.

def check_cert_notes():
    print("\n[5] Certification notes match the actual reviewer flow")
    p = HERE / "docs" / "MICROSOFT_STORE_CERT_NOTES.md"
    if not p.exists():
        record("cert notes file present", False, "not found")
        return
    t = p.read_text(encoding="utf-8", errors="replace")
    record("notes state no account/sign-in required",
           re.search(r"no (account|sign-in|login)", t, re.I) is not None, "stated")
    record("notes describe on-device AI plan (no network needed for the plan)",
           re.search(r"on-device", t, re.I) is not None
           and re.search(r"no (internet|network)", t, re.I) is not None,
           "on-device AI + no-network-for-plan stated")
    record("notes carry the reviewer $0 checkout code (%s)" % CERT_CODE,
           CERT_CODE in t, "cert code present in notes" if CERT_CODE in t else "cert code missing")
    record("notes give the UAC -> scan -> Perform Repairs flow",
           ("UAC" in t) and ("Perform Repairs" in t), "reviewer flow present")


# ── 6. The final SUBMISSION PACKAGE is the authority (PKG-001) ────────────────

def find_submission_package(explicit):
    if explicit:
        p = Path(explicit)
        return p if p.exists() else None
    skip = ("node_modules", "package-layout")
    ups = [p for p in HERE.rglob("*.msixupload") if not any(s in str(p) for s in skip)]
    if ups:
        return max(ups, key=lambda p: p.stat().st_mtime)
    msix = [p for p in HERE.rglob("*.msix") if not any(s in str(p) for s in skip)]
    return max(msix, key=lambda p: p.stat().st_mtime) if msix else None


def _package_entries(path, depth=0):
    """Return (entries: {name: size}, version, note) for the INNERMOST app package,
    descending through a .msixupload/.appxbundle wrapper if needed."""
    try:
        with zipfile.ZipFile(path) as z:
            infos = z.infolist()
            manifest = [i for i in infos if i.filename.lower().endswith("appxmanifest.xml")]
            nested = [i for i in infos if i.filename.lower().endswith((".msix", ".appx"))]
            if manifest:
                entries = {i.filename: i.file_size for i in infos}
                version = None
                try:
                    xml = z.read(manifest[0].filename).decode("utf-8", "replace")
                    m = re.search(r'Identity[^>]*\bVersion="([0-9.]+)"', xml)
                    version = m.group(1) if m else None
                except Exception:
                    pass
                return entries, version, ""
            if nested and depth < 3:
                biggest = max(nested, key=lambda i: i.file_size)
                with tempfile.TemporaryDirectory() as td:
                    inner = Path(td) / Path(biggest.filename).name
                    with z.open(biggest.filename) as src, open(inner, "wb") as dst:
                        shutil.copyfileobj(src, dst, 1 << 20)
                    return _package_entries(inner, depth + 1)
            return {i.filename: i.file_size for i in infos}, None, "no AppxManifest found"
    except Exception as e:
        return {}, None, "inspect error: %s" % str(e)[:80]


def check_package(pkg):
    print("\n[6] The final SUBMISSION PACKAGE is inspected directly  (PKG-001)")
    if pkg is None:
        record("candidate submission package found", False,
               "no .msixupload/.msix found — build the Store package first")
        return None, None
    sha = sha256_file(pkg)
    size_mb = pkg.stat().st_size / 1e6
    record("candidate submission package found", True,
           "%s (%.1f MB)" % (pkg.name, size_mb))
    print("         path: %s" % pkg)
    print("         sha256: %s" % sha)

    entries, pkg_ver, note = _package_entries(pkg)
    if note:
        record("package is a readable app package", False, note)
        return pkg, sha

    # Version must match the source manifest — catches a STALE package (the exact
    # gap where a fresh source head is shipped inside an old .msixupload).
    src_ver = None
    if SOURCE_MANIFEST.exists():
        mm = re.search(r'Identity[^>]*\bVersion="([0-9.]+)"',
                       SOURCE_MANIFEST.read_text(encoding="utf-8", errors="replace"))
        src_ver = mm.group(1) if mm else None
    record("package version matches source manifest", bool(pkg_ver) and pkg_ver == src_ver,
           "package=%s source=%s" % (pkg_ver, src_ver))

    # The cert-critical model assets must be INSIDE the package, not just in source.
    def has(suffix, min_size=0):
        for name, sz in entries.items():
            n = name.replace("\\", "/")
            if ("models/%s/" % MODEL_NAME) in n and n.endswith(suffix) and sz >= min_size:
                return sz
        return None

    cfg = has("genai_config.json")
    record("model genai_config.json inside package", cfg is not None,
           "found" if cfg is not None else "MISSING inside %s" % pkg.name)
    w = has("model.onnx.data", 100_000_000)
    record("model weights (>100MB) inside package", w is not None,
           "%.0f MB" % (w / 1e6) if w else "MISSING/too small inside %s" % pkg.name)
    prov = has("aegis-formation-manifest.json")
    record("formation provenance inside package", prov is not None,
           "found" if prov is not None else "MISSING inside %s" % pkg.name)
    return pkg, sha


# ── 7. Windows App Certification Kit (WACK) ──────────────────────────────────

def run_wack(pkg, attempt):
    print("\n[7] Windows App Certification Kit (WACK)")
    if not WACK_EXE.exists():
        record_manual("WACK run against the package",
                      "appcert.exe not found — install the Windows App Certification Kit and run it")
        return
    if pkg is None:
        record_manual("WACK run against the package", "no candidate package to test")
        return
    cmd_hint = '"%s" test -appxpackagepath "%s" -reportoutputpath wack-report.xml' % (WACK_EXE, pkg)
    if not attempt:
        record_manual("WACK run against the package",
                      "appcert present but NOT auto-run (needs an ELEVATED shell + minutes). Run: " + cmd_hint)
        return
    try:
        out = subprocess.run([str(WACK_EXE), "test", "-appxpackagepath", str(pkg),
                              "-reportoutputpath", str(HERE / "wack-report.xml")],
                             capture_output=True, text=True, timeout=1800)
        report = HERE / "wack-report.xml"
        overall = None
        if report.exists():
            rt = report.read_text(encoding="utf-8", errors="replace")
            m = re.search(r'OVERALL_RESULT="([A-Z]+)"', rt)
            overall = m.group(1) if m else None
        if out.returncode == 0 and overall == "PASS":
            record("WACK PASS", True, "OVERALL_RESULT=PASS")
        elif overall:
            record("WACK PASS", False, "OVERALL_RESULT=%s (see wack-report.xml)" % overall)
        else:
            record_manual("WACK run against the package",
                          "appcert exit=%s — likely needs elevation; run manually: %s" % (out.returncode, cmd_hint))
    except subprocess.TimeoutExpired:
        record("WACK PASS", False, "TIMEOUT after 30 min")
    except Exception as e:
        record_manual("WACK run against the package",
                      "could not auto-run (%s); run manually: %s" % (str(e)[:60], cmd_hint))


# ── evidence report + summary ────────────────────────────────────────────────

def write_evidence(path, mode, pkg, pkg_sha):
    blocking = [r for r in RESULTS if r["state"] == "fail"]
    warns = [r for r in RESULTS if r["state"] == "warn"]
    manual = [r for r in RESULTS if r["state"] == "manual"]
    status = "CERT_READY" if (not blocking and not manual) else "DO_NOT_SUBMIT"
    doc = {
        "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "preflight_version": PREFLIGHT_VERSION,
        "mode": mode,
        "package_path": str(pkg) if pkg else None,
        "package_sha256": pkg_sha,
        "checks": RESULTS,
        "blocking_failures": [r["claim"] for r in blocking],
        "warnings": [r["claim"] for r in warns],
        "manual_required": [r["claim"] for r in manual],
        "final_status": status,
    }
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(doc, indent=2), encoding="utf-8")
    except Exception as e:
        print("  (could not write evidence to %s: %s)" % (path, e))
    return status, blocking, warns, manual


def main(argv=None):
    ap = argparse.ArgumentParser(description="ProntoPC Store certification preflight v2")
    ap.add_argument("--mode", choices=["store"], default="store",
                    help="store (default). Web/Stripe/Cloud-Run gates are PART of the store "
                         "product (it is paid), so there is no separate legacy-web mode.")
    ap.add_argument("--package", help="explicit .msix/.msixupload to inspect (default: newest found)")
    ap.add_argument("--no-smoke", action="store_true", help="skip the (slow) ModelSmoke execution")
    ap.add_argument("--wack", action="store_true", help="attempt an automated WACK run (needs elevation)")
    ap.add_argument("--evidence-out", default=str(DEFAULT_EVIDENCE),
                    help="path for the machine-readable evidence JSON")
    args = ap.parse_args(argv)

    print("=" * 74)
    print("ProntoPC — MICROSOFT STORE CERTIFICATION PRE-FLIGHT  v%s  (mode=%s)"
          % (PREFLIGHT_VERSION, args.mode))
    print("=" * 74)

    print_policy_note()
    key = stripe_key()
    check_reviewer_access(key)
    check_success_path()
    check_ai_repair_plan()
    run_model_smoke(not args.no_smoke)
    check_countable_claims()
    check_cert_notes()
    pkg = find_submission_package(args.package)
    pkg, pkg_sha = check_package(pkg)
    run_wack(pkg, args.wack)

    status, blocking, warns, manual = write_evidence(
        Path(args.evidence_out), args.mode, pkg, pkg_sha)

    passed = sum(1 for r in RESULTS if r["ok"])
    print("\n" + "=" * 74)
    print("%d/%d checks passed" % (passed, len(RESULTS)))
    if warns:
        print("\nNON-BLOCKING WARNINGS:")
        for r in warns:
            print("  - %s: %s" % (r["claim"], r["evidence"]))
    if manual:
        print("\nMANUAL VERIFICATION REQUIRED (blocking until done):")
        for r in manual:
            print("  ! %s" % r["claim"])
            print("      %s" % r["evidence"])
    if blocking:
        print("\nBLOCKING FAILURES:")
        for r in blocking:
            print("  X %s" % r["claim"])
            print("      %s" % r["evidence"])

    print("\nEvidence: %s" % args.evidence_out)
    print("FINAL STATUS: %s" % status)
    print("=" * 74)
    return 0 if status == "CERT_READY" else 1


if __name__ == "__main__":
    sys.exit(main())
