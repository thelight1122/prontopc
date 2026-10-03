"""
ProntoPC — Microsoft Store certification pre-flight  (v3).

Tests every FUNCTIONAL CLAIM the Store listing makes — and EXECUTES the behaviours
a machine can, rather than trusting that a source file exists. Exits non-zero if any
blocking claim cannot be demonstrated, or if a blocking gate can only be satisfied by
a manual step that has not been done.

The point of this file: "ready to submit" stops being anyone's judgement and becomes a
test result.

PRODUCT SHAPE (v3 — the one-time paid Store app pivot, docs/PROPOSAL-ONE-TIME-PAID-APP.md):
  • ProntoPC is a SELF-CONTAINED, ONE-TIME PAID Microsoft Store app ($9.99).
  • NO external payment, NO account/sign-in, NO server on the critical path. The 2026-09-17
    rejection (10.3.1 "could not reach our server" + 10.8.4 pricing) was caused by the old
    external Stripe/Cloud-Run pay-per-run gate; that gate is removed. This preflight now
    BLOCKS if any Stripe/checkout/token/Cloud-Run gate is still present in the shipping
    surfaces — the opposite of the v2 posture, on purpose.
  • The repair PLAN is authored ON-DEVICE by a bundled Gemma model, with NO cloud fallback;
    the completion report is generated ON-DEVICE. No network is needed to run the product.

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
import zipfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
CLAIMED_CATEGORIES = 25      # "25-category diagnostic scan" (listing)
CLAIMED_REPAIRS = 18         # "18 repair capabilities" (listing)
STORE_PRICE = "$9.99"        # one-time base price (10.8.4)

PREFLIGHT_VERSION = "3.4"
MODEL_NAME = "gemma-3-1b-it-formed-v0.2.1"
MODEL_DIR = HERE / "StoreApp" / "Assets" / "models" / MODEL_NAME
SOURCE_MANIFEST = HERE / "StoreApp" / "Package.appxmanifest"
SOURCE_CSPROJ = HERE / "StoreApp" / "ProntoPC.csproj"
DEFAULT_EVIDENCE = HERE / "docs" / "CERTIFICATION_PREFLIGHT_EVIDENCE.json"
WACK_EXE = Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) \
    / "Windows Kits" / "10" / "App Certification Kit" / "appcert.exe"
VCLIBS_DESKTOP_NAME = "Microsoft.VCLibs.140.00.UWPDesktop"
VCLIBS_DESKTOP_MIN_VERSION = "14.0.33728.0"
VCLIBS_DESKTOP_PUBLISHER = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"
ONNX_NATIVE_DLLS = {"onnxruntime.dll", "onnxruntime-genai.dll", "onnxruntime_providers_shared.dll"}
VC_RUNTIME_IMPORTS = {"MSVCP140.DLL", "MSVCP140_1.DLL", "VCRUNTIME140.DLL", "VCRUNTIME140_1.DLL"}

# Historical Microsoft certification rejection references — the concrete failures this
# preflight regresses against. Labelled HISTORICAL so nobody mistakes an old policy number
# for a guarantee of the current policy text.
HISTORICAL_POLICY = [
    ("10.3.1",    "reviewer could not test: primary functionality required a server/test account, and the app hit 'could not reach our server' (fixed: one-time paid Store app, no server, no account)"),
    ("10.8.4",    "purchase types/prices missing or inaccurate (fixed: single one-time Store base price $9.99, no external or in-app purchase)"),
    ("10.1.2.10", "'repair plan built by AI' — on-device authoring, no cloud fallback (still in force)"),
]

# Payment/server markers that MUST NOT appear in the shipping surfaces anymore. If any of
# these is present in the served site or the app, the external gate is not fully removed and
# submission is blocked.
FORBIDDEN_GATE_MARKERS = [
    "buy.stripe.com", "stripe", "checkout.stripe", "prontopc://run", "await-payment",
    "verify-payment", "verify-token", "client_reference_id",
]
SHIPPING_HTML = ["index.html", "terms.html", "privacy.html"]

RESULTS = []


# ── result recording ─────────────────────────────────────────────────────────
def record(claim, ok, evidence, blocking=True):
    state = "pass" if ok else ("fail" if blocking else "warn")
    RESULTS.append({"claim": claim, "ok": bool(ok), "evidence": str(evidence)[:300],
                    "blocking": blocking, "state": state, "manual": False})
    mark = {"pass": "PASS", "fail": "FAIL", "warn": "WARN"}[state]
    print("  [%-4s] %-48s %s" % (mark, claim[:48], str(evidence)[:100]))


def record_manual(claim, note):
    RESULTS.append({"claim": claim, "ok": False, "evidence": str(note)[:300],
                    "blocking": True, "state": "manual", "manual": True})
    print("  [%-4s] %-48s %s" % ("MANL", claim[:48], str(note)[:100]))


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
        print("  [HIST] %-10s %s" % (code, desc[:98]))
    print("  [NOTE] Re-check the CURRENT Microsoft Store policy text at submission time.")


# ── 1. The external payment/server gate is GONE (10.3.1) ─────────────────────
def check_no_external_gate():
    print("\n[1] No external payment/server gate remains in shipping surfaces  (10.3.1)")
    surfaces = []
    for name in SHIPPING_HTML:
        p = HERE / name
        if p.exists():
            surfaces.append(p)
    app = HERE / "StoreApp"
    if app.exists():
        surfaces += [p for p in app.rglob("*.cs")]
        surfaces += [p for p in app.rglob("*.xaml")]

    hits = {}
    for p in surfaces:
        try:
            t = p.read_text(encoding="utf-8", errors="replace").lower()
        except Exception:
            continue
        found = [m for m in FORBIDDEN_GATE_MARKERS if m in t]
        if found:
            hits[str(p.relative_to(HERE))] = sorted(set(found))

    record("served site has no Stripe/checkout/token gate",
           not any(h in hits for h in SHIPPING_HTML),
           "clean" if not any(h in hits for h in SHIPPING_HTML)
           else "; ".join("%s:%s" % (k, ",".join(v)) for k, v in hits.items() if k in SHIPPING_HTML))

    app_hits = {k: v for k, v in hits.items() if k.startswith("StoreApp")}
    record("app has no Stripe/checkout/token/Cloud-Run gate",
           not app_hits,
           "clean" if not app_hits
           else "still present in " + "; ".join("%s:%s" % (k, ",".join(v)) for k, v in app_hits.items()))


# ── 2. "AI-powered repair plan" model bundled in source (10.1.2.10) ──────────
def check_ai_repair_plan():
    print("\n[2] \"AI-powered repair plan\" model bundled in source  (10.1.2.10)")
    cfg = MODEL_DIR / "genai_config.json"
    weights = MODEL_DIR / "model.onnx.data"
    formation = MODEL_DIR / "aegis-formation-manifest.json"
    have_model = cfg.exists() and weights.exists() and weights.stat().st_size > 100_000_000
    record("on-device model bundled (genai_config + weights)", have_model,
           str(MODEL_DIR) if have_model else "MISSING at %s" % MODEL_DIR)
    record("formed responder carries formation provenance", formation.exists(),
           str(formation) if formation.exists() else "formation manifest missing", blocking=False)


# ── 2b. EXECUTE the on-device authoring ───────────────────────────────────────
def run_model_smoke(enabled):
    print("\n[2b] ModelSmoke EXECUTES real on-device authoring  (AI-001)")
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
        record("ModelSmoke PASS (on-device plan + admissible action)", False, "TIMEOUT after 15 min")
        return
    dur = time.time() - t0
    stdout = out.stdout or ""
    ok = out.returncode == 0 and "MODEL SMOKE PASS" in stdout
    tail = stdout.strip().splitlines()[-1] if stdout.strip() else "(no output)"
    record("ModelSmoke PASS (on-device plan + admissible action)", ok,
           "exit=%s %.0fs — %s" % (out.returncode, dur, tail[:80]))


# ── 3. Countable claims made in the listing ──────────────────────────────────
def check_countable_claims():
    print("\n[3] Countable listing claims match the code")
    # Diagnostic categories — BLOCKING: the "25-category" listing claim can cause rejection.
    diag = HERE / "StoreApp" / "Diagnostics.cs"
    if diag.exists():
        d = diag.read_text(encoding="utf-8", errors="replace")
        cats = set(re.findall(r'\["([a-z_]+)"\]\s*=', d))
        record("diagnostic categories >= %d claimed" % CLAIMED_CATEGORIES,
               len(cats) >= CLAIMED_CATEGORIES, "%d categories found" % len(cats))
    else:
        record("Diagnostics.cs present", False, "not found", blocking=False)

    # Repair capabilities — the catalogue is enforced by the app's Envelope. Non-blocking
    # while the app is mid-pivot; still surfaced so a shrunk catalogue is visible.
    ids = set()
    env = HERE / "StoreApp" / "Envelope.cs"
    if env.exists():
        e = env.read_text(encoding="utf-8", errors="replace")
        ids = set(re.findall(r'"([a-z][a-z0-9_]{3,})"', e))
    record("repair capabilities >= %d claimed" % CLAIMED_REPAIRS,
           len(ids) >= CLAIMED_REPAIRS,
           "%d candidate action IDs in Envelope.cs" % len(ids), blocking=False)


# ── 4. Certification notes match the actual (server-free) reviewer flow ───────
def check_cert_notes():
    print("\n[4] Certification notes match the one-time-paid, no-account, offline flow")
    p = HERE / "docs" / "MICROSOFT_STORE_CERT_NOTES.md"
    if not p.exists():
        record("cert notes file present", False, "not found")
        return
    t = p.read_text(encoding="utf-8", errors="replace")
    normalized = re.sub(r"\s+", " ", t)
    tl = normalized.lower()

    no_account = ("no account" in tl) or ("no code, account" in tl) or ("account, sign-in" in tl) or ("no sign-in" in tl)
    addresses_test_account = ("test account" in tl) or ("test credentials" in tl)
    record("notes state no account/sign-in/test-account required",
           no_account and addresses_test_account, "no-account + test-account addressed")
    record("notes state on-device AI + offline (no server/network)",
           ("on-device" in tl) and (("no server" in tl) or ("no internet" in tl) or ("offline" in tl)),
           "on-device + offline stated")
    record("notes state one-time paid Store purchase (10.8.4)",
           ("one-time" in tl) and (STORE_PRICE in normalized) and ("microsoft store" in tl),
           "one-time %s via Microsoft Store" % STORE_PRICE)
    record("notes state no subscription/in-app/add-on/trial (10.8.4)",
           ("no subscription" in tl) and ("no in-app purchase" in tl or "no in-app" in tl)
           and ("no add-on" in tl) and ("no trial" in tl),
           "no subscription/IAP/add-on/trial stated")
    record("notes give launch -> scan -> repair-time UAC flow",
           (("no uac prompt on launch" in tl) or ("no uac prompt" in tl) or ("no administrator prompt on launch" in tl))
           and ("perform repairs" in tl)
           and (("administrator approval" in tl) or ("administrator permission" in tl)),
           "reviewer flow present")
    # The stale external-gate flow must be GONE from the pasteable reviewer notes. (The long
    # notes legitimately EXPLAIN the removed Stripe gate in their "what changed" records section,
    # so the strict scan targets the short paste that actually goes to the reviewer.)
    short = HERE / "docs" / "CERT_NOTES_SHORT.txt"
    st = short.read_text(encoding="utf-8", errors="replace").lower() if short.exists() else ""
    stale = [m for m in ("stripe", "mscert", "prontopc://run", "buy.stripe",
                         "does not use the microsoft store commerce", "$29", "per run") if m in st]
    record("reviewer paste (CERT_NOTES_SHORT) carries NO stale pay-per-run flow",
           short.exists() and not stale,
           "clean" if (short.exists() and not stale)
           else ("short notes missing" if not short.exists() else "stale: %s" % ", ".join(stale)))


# ── 5. The final SUBMISSION PACKAGE is the authority (PKG-001) ────────────────
def find_submission_package(explicit):
    if explicit:
        p = Path(explicit)
        return p if p.exists() else None
    skip = ("node_modules", "package-layout", "archive")
    ups = [p for p in HERE.rglob("*.msixupload") if not any(s in str(p) for s in skip)]
    if ups:
        return max(ups, key=lambda p: p.stat().st_mtime)
    msix = [p for p in HERE.rglob("*.msix") if not any(s in str(p) for s in skip)]
    return max(msix, key=lambda p: p.stat().st_mtime) if msix else None


def _package_entries(path, depth=0):
    try:
        with zipfile.ZipFile(path) as z:
            infos = z.infolist()
            manifest = [i for i in infos if i.filename.lower().endswith("appxmanifest.xml")]
            nested = [i for i in infos if i.filename.lower().endswith((".msix", ".appx"))]
            if manifest:
                entries = {i.filename: i.file_size for i in infos}
                version = None
                xml = ""
                try:
                    xml = z.read(manifest[0].filename).decode("utf-8", "replace")
                    m = re.search(r'Identity[^>]*\bVersion="([0-9.]+)"', xml)
                    version = m.group(1) if m else None
                except Exception:
                    pass
                return entries, version, xml, ""
            if nested and depth < 3:
                biggest = max(nested, key=lambda i: i.file_size)
                with tempfile.TemporaryDirectory() as td:
                    inner = Path(td) / Path(biggest.filename).name
                    with z.open(biggest.filename) as src, open(inner, "wb") as dst:
                        shutil.copyfileobj(src, dst, 1 << 20)
                    return _package_entries(inner, depth + 1)
            return {i.filename: i.file_size for i in infos}, None, "", "no AppxManifest found"
    except Exception as e:
        return {}, None, "", "inspect error: %s" % str(e)[:80]


def _version_tuple(v):
    return tuple(int(x) for x in re.findall(r"\d+", v or ""))


def _version_parts(v):
    parts = [int(x) for x in re.findall(r"\d+", v or "")]
    return parts + [0] * (4 - len(parts))


def _version_at_least(found, required):
    a = list(_version_tuple(found))
    b = list(_version_tuple(required))
    width = max(len(a), len(b))
    a += [0] * (width - len(a))
    b += [0] * (width - len(b))
    return tuple(a) >= tuple(b)


def _package_dependencies(manifest_xml):
    deps = []
    for m in re.finditer(r"<PackageDependency\b([^>]*)/?\s*>", manifest_xml or "", re.I):
        attrs = {k: v for k, v in re.findall(r'(\w+)="([^"]*)"', m.group(1))}
        deps.append(attrs)
    return deps


def _has_vclibs_dependency(manifest_xml):
    for dep in _package_dependencies(manifest_xml):
        if dep.get("Name") != VCLIBS_DESKTOP_NAME:
            continue
        min_version = dep.get("MinVersion", "")
        publisher = dep.get("Publisher", "")
        version_ok = _version_at_least(min_version, VCLIBS_DESKTOP_MIN_VERSION)
        publisher_ok = publisher == VCLIBS_DESKTOP_PUBLISHER
        return version_ok and publisher_ok, "Name=%s MinVersion=%s Publisher=%s" % (
            dep.get("Name", ""), min_version or "(missing)", publisher or "(missing)")
    return False, "missing %s PackageDependency" % VCLIBS_DESKTOP_NAME


def _wack_package_for(pkg, pkg_ver):
    if pkg is None:
        return None
    if str(pkg).lower().endswith((".msixupload", ".appxupload")):
        sibling = pkg.with_suffix(".msix")
        if sibling.exists():
            _, sibling_ver, _, _ = _package_entries(sibling)
            if sibling_ver == pkg_ver:
                return sibling
    return pkg


def check_vclibs_runtime_coverage(entries, manifest_xml):
    basenames = {Path(name.replace("\\", "/")).name.upper() for name in entries}
    has_onnx_native = any(name.upper() in {d.upper() for d in ONNX_NATIVE_DLLS} for name in basenames)
    if not has_onnx_native:
        record("ONNX native VC++ runtime dependency covered", True,
               "no ONNX Runtime native DLLs detected in package")
        return

    packaged_imports = sorted(VC_RUNTIME_IMPORTS.intersection(basenames))
    missing_packaged = sorted(VC_RUNTIME_IMPORTS.difference(basenames))
    if not missing_packaged:
        record("ONNX native VC++ runtime dependency covered", True,
               "VC runtime DLLs packaged: %s" % ", ".join(packaged_imports))
        return

    dep_ok, dep_evidence = _has_vclibs_dependency(manifest_xml)
    record("ONNX native VC++ runtime dependency covered", False,
           ("%s; package must include clean-machine VC runtime DLLs beside ProntoPC.exe; missing: %s" %
            (dep_evidence if dep_ok else dep_evidence, ", ".join(missing_packaged))))


def _declared_wpf_resources():
    if not SOURCE_CSPROJ.exists():
        return []
    text = SOURCE_CSPROJ.read_text(encoding="utf-8", errors="replace")
    found = []
    for item in ("SplashScreen", "Resource"):
        for match in re.finditer(r"<%s\b[^>]*\bInclude=\"([^\"]+)\"" % item, text, re.I):
            found.append((item, match.group(1).replace("\\", "/").lower()))
    return found


def check_wpf_compiled_resources():
    declared = _declared_wpf_resources()
    if not declared:
        record("WPF compiled resources contain declared images", True,
               "no WPF SplashScreen/Resource image declarations found")
        return

    obj_root = HERE / "StoreApp" / "obj"
    resources = sorted(obj_root.glob("**/ProntoPC.g.resources"),
                       key=lambda p: p.stat().st_mtime if p.exists() else 0,
                       reverse=True)
    if not resources:
        record("WPF compiled resources contain declared images", False,
               "MISSING ProntoPC.g.resources; run StoreApp/build.ps1 before packaging")
        return

    ps = (
        "$reader = [System.Resources.ResourceReader]::new('%s'); "
        "$names = @(); foreach ($entry in $reader) { $names += $entry.Key }; "
        "$reader.Close(); $names"
    ) % str(resources[0]).replace("'", "''")
    try:
        proc = subprocess.run(["powershell", "-NoProfile", "-Command", ps],
                              cwd=str(HERE), text=True, capture_output=True, timeout=30)
        names = {line.strip().lower() for line in proc.stdout.splitlines() if line.strip()}
        reader_note = proc.stderr.strip() if proc.returncode else ""
    except Exception as exc:
        names = set()
        reader_note = str(exc)

    missing = [path for _item, path in declared if path not in names]
    record("WPF compiled resources contain declared images", not missing,
           ("found %d declared resource path(s) in %s" % (len(declared), resources[0])) if not missing else
           ("MISSING in %s: %s%s" % (
               resources[0], ", ".join(missing),
               ("; reader=%s" % reader_note) if reader_note else "")))


def check_package(pkg):
    print("\n[5] The final SUBMISSION PACKAGE is inspected directly  (PKG-001)")
    if pkg is None:
        record_manual("candidate submission package found",
                      "no .msixupload/.msix found — build the one-time-paid Store package first")
        return None, None
    sha = sha256_file(pkg)
    size_mb = pkg.stat().st_size / 1e6
    record("candidate submission package found", True, "%s (%.1f MB)" % (pkg.name, size_mb))
    print("         path: %s" % pkg)
    print("         sha256: %s" % sha)

    entries, pkg_ver, manifest_xml, note = _package_entries(pkg)
    if note:
        record("package is a readable app package", False, note)
        return pkg, sha

    src_ver = None
    if SOURCE_MANIFEST.exists():
        mm = re.search(r'Identity[^>]*\bVersion="([0-9.]+)"',
                       SOURCE_MANIFEST.read_text(encoding="utf-8", errors="replace"))
        src_ver = mm.group(1) if mm else None
    record("package version matches source manifest", bool(pkg_ver) and pkg_ver == src_ver,
           "package=%s source=%s" % (pkg_ver, src_ver))
    parts = _version_parts(pkg_ver)
    fourth_ok = bool(pkg_ver) and len(parts) >= 4 and parts[3] == 0
    record("Store package version leaves fourth section reserved",
           fourth_ok,
           "package=%s; Windows 10/11 Store packages must end in .0" % (pkg_ver or "(missing)"))

    def has(suffix, min_size=0):
        for name, sz in entries.items():
            n = name.replace("\\", "/")
            if ("models/%s/" % MODEL_NAME) in n and n.endswith(suffix) and sz >= min_size:
                return sz
        return None

    record("model genai_config.json inside package", has("genai_config.json") is not None,
           "found" if has("genai_config.json") is not None else "MISSING inside %s" % pkg.name)
    w = has("model.onnx.data", 100_000_000)
    record("model weights (>100MB) inside package", w is not None,
           "%.0f MB" % (w / 1e6) if w else "MISSING/too small inside %s" % pkg.name)
    check_wpf_compiled_resources()
    check_vclibs_runtime_coverage(entries, manifest_xml)
    return pkg, sha


# ── 6. Windows App Certification Kit (WACK) ──────────────────────────────────
def run_wack(pkg, attempt):
    print("\n[6] Windows App Certification Kit (WACK)")
    if not WACK_EXE.exists():
        record_manual("WACK run against the package",
                      "appcert.exe not found — install the Windows App Certification Kit and run it")
        return
    if pkg is None:
        record_manual("WACK run against the package", "no candidate package to test")
        return
    _, pkg_ver, _, _ = _package_entries(pkg)
    wack_pkg = _wack_package_for(pkg, pkg_ver)
    existing_reports = [
        HERE / "wack-report-current.xml",
        HERE / ("wack-report-%s.xml" % "".join((pkg_ver or "").split(".")[:3])),
        HERE / "wack-report.xml",
    ]
    pkg_mtime = wack_pkg.stat().st_mtime if wack_pkg else pkg.stat().st_mtime
    stale_reports = []
    for report in existing_reports:
        if not report.exists():
            continue
        rt = report.read_text(encoding="utf-8", errors="replace")
        overall = re.search(r'OVERALL_RESULT="([A-Z]+)"', rt)
        app_version = re.search(r'APP_VERSION="([0-9.]+)"', rt)
        if overall and overall.group(1) == "PASS" and app_version and app_version.group(1) == pkg_ver:
            if report.stat().st_mtime < pkg_mtime:
                stale_reports.append(report.name)
                continue
            record("WACK run against the package", True,
                   "%s: OVERALL_RESULT=PASS; APP_VERSION=%s; sha256=%s" %
                   (report.name, pkg_ver, sha256_file(report)))
            return
    if stale_reports:
        record_manual("WACK run against the package",
                      "existing PASS report(s) predate the rebuilt package and must be rerun: " + ", ".join(stale_reports))
        return
    cmd_hint = '"%s" test -appxpackagepath "%s" -reportoutputpath wack-report.xml' % (WACK_EXE, wack_pkg or pkg)
    if not attempt:
        record_manual("WACK run against the package",
                      "appcert present but NOT auto-run (needs an ELEVATED shell + minutes). Run: " + cmd_hint)
        return
    try:
        out = subprocess.run([str(WACK_EXE), "test", "-appxpackagepath", str(wack_pkg or pkg),
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
    ap = argparse.ArgumentParser(description="ProntoPC Store certification preflight v3")
    ap.add_argument("--mode", choices=["store"], default="store")
    ap.add_argument("--package", help="explicit .msix/.msixupload to inspect (default: newest found)")
    ap.add_argument("--no-smoke", action="store_true", help="skip the (slow) ModelSmoke execution")
    ap.add_argument("--wack", action="store_true", help="attempt an automated WACK run (needs elevation)")
    ap.add_argument("--evidence-out", default=str(DEFAULT_EVIDENCE))
    args = ap.parse_args(argv)

    print("=" * 74)
    print("ProntoPC — MICROSOFT STORE CERTIFICATION PRE-FLIGHT  v%s  (mode=%s)"
          % (PREFLIGHT_VERSION, args.mode))
    print("=" * 74)

    print_policy_note()
    check_no_external_gate()
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
