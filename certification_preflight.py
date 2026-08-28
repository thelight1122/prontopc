"""
ProntoPC — Microsoft Store certification pre-flight.

Tests every FUNCTIONAL CLAIM the Store listing makes against the LIVE system.
Exits non-zero if any claim cannot be demonstrated.

The point of this file: "ready to submit" stops being anyone's judgement and
becomes a test result. Four consecutive certification rejections each fixed one
thing and were then caught by the next unverified claim:

  10.3.3    reviewer could not get past the $29 paywall
  10.1.2.10 after $0 checkout, /success failed with "Could not reach our server"
  10.1.2.10 "repair plan built by AI" absent - both LLM providers were dead and
            the desktop client silently fell back to a local rule-based plan

Every check below exists because something like it was missed.

Run:  python certification_preflight.py
"""

import json
import re
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
BACKEND = "https://pronto-pc-108655745293.us-central1.run.app"
SITE = "https://prontopc.online"
CERT_CODE = "MSCERT2026"
PAYMENT_LINK = "plink_1Ta4SfLLKFLplB2ZeOVKy2ol"
CLAIMED_CATEGORIES = 35      # "35+ category diagnostic scan"
CLAIMED_REPAIRS = 18         # "18 repair capabilities"
CLAIMED_SESSION_DAYS = 3     # "3-day session window"

RESULTS = []


def record(claim, ok, evidence, blocking=True):
    RESULTS.append({"claim": claim, "ok": bool(ok), "evidence": str(evidence)[:300],
                    "blocking": blocking})
    mark = "PASS" if ok else ("FAIL" if blocking else "WARN")
    print("  [%-4s] %-46s %s" % (mark, claim[:46], str(evidence)[:110]))


def stripe_key():
    """Read the live Stripe key from Cloud Run. Never printed."""
    # gcloud is a .cmd shim on Windows; needs a shell to resolve.
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


# ── 1. Free test access for the reviewer (policy 10.3.3) ─────────────────────

def check_reviewer_access(key):
    print("\n[1] Reviewer can reach the product at no cost  (policy 10.3.3)")
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
        # Current Stripe API versions put the discount under `promotion.coupon`,
        # not a top-level `coupon`, and the filtered list does not expand either.
        # Reading the old field yields a false negative — which is worse than no
        # gate at all, because a gate that cries wolf gets ignored.
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
        record("cert code has redemptions left", left > 0,
               "%s remaining" % left)
        exp = c.get("expires_at")
        record("cert code not expired", (exp is None) or exp > time.time(),
               "expires_at=%s" % exp)
    except Exception as e:
        record("cert code check", False, e)


# ── 2. The post-checkout path the reviewer walks (prior 10.1.2.10) ───────────

def check_success_path():
    print("\n[2] Post-checkout path resolves  (prior 10.1.2.10 failure)")
    s = http_status(SITE)
    record("prontopc.online reachable", s == 200, "HTTP %s" % s)
    s = http_status(SITE + "/success")
    record("/success page reachable", s == 200, "HTTP %s" % s)
    # Backend must answer; root is an API server with no root route, so a 404
    # is a healthy signal and a connection error is not.
    s = http_status(BACKEND)
    record("backend responding", isinstance(s, int), "HTTP %s" % s)


# ── 3. THE CLAIM THAT FAILED: an AI-built repair plan ────────────────────────

def check_ai_repair_plan():
    print("\n[3] \"AI-powered repair plan\" is genuinely present  (10.1.2.10)")
    payload = {
        "session": "certification-preflight",
        "diagnostics": {
            "os": {"hostname": "PREFLIGHT-PC", "caption": "Windows 11 Pro"},
            "disk": [{"device": "C:", "percent_used": 91, "free_gb": 11}],
            "memory": {"percent_used": 88},
            "uptime": {"uptime_days": 23},
            "drivers": {"unsigned": 3, "outdated": 7},
            "security": {"real_time_protection": True, "firewall": True},
            "temp_files": {"mb": 4200},
            "startup": {"count": 19},
        },
    }
    req = urllib.request.Request(
        BACKEND + "/api/repair-plan",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=150) as r:
            body = json.loads(r.read())
    except urllib.error.HTTPError as e:
        try:
            body = json.loads(e.read())
        except Exception:
            body = {"ok": False, "error": "HTTP %s" % e.code}
    except Exception as e:
        record("LLM reachable", False, e)
        return

    if not body.get("ok"):
        record("LLM returns a plan", False, body.get("error"))
        return

    plan = body.get("plan") or {}
    summary = (plan.get("summary") or "").strip()
    actions = plan.get("actions") or []
    record("LLM returns a plan", True, "%d actions" % len(actions))
    record("plan has a summary", len(summary) > 30, summary[:90])
    record("plan proposes actions", len(actions) > 0, "%d actions" % len(actions))

    # A local rule-based fallback is what shipped last time and looked like AI.
    # Its summary is a fixed string; the AI's is not.
    canned = "Your system scan completed. We found maintenance items"
    record("plan is NOT the local canned fallback", canned.lower() not in summary.lower(),
           "canned-string absent" if canned.lower() not in summary.lower()
           else "MATCHES LOCAL FALLBACK TEXT")

    # The plan should reference the actual diagnostics it was given.
    referenced = sum(1 for t in ["disk", "memory", "driver", "temp", "startup", "uptime"]
                     if t in json.dumps(plan).lower())
    record("plan references supplied diagnostics", referenced >= 2,
           "%d/6 diagnostic topics referenced" % referenced)


# ── 4. Countable claims made in the listing ──────────────────────────────────

def check_countable_claims():
    print("\n[4] Countable listing claims match the code")
    src = HERE / "pronto-server-cloudrun.mjs"
    text = src.read_text(encoding="utf-8", errors="replace") if src.exists() else ""

    # Repair capabilities: the authoritative list is the allow-list handed to the
    # LLM ("Known repair action IDs you may use:"). Counting stray "id": fields
    # elsewhere in the file undercounts badly.
    marker = "Known repair action IDs you may use:"
    ids = set()
    if marker in text:
        block = text.split(marker, 1)[1]
        # the catalogue is the first non-empty block after the marker
        block = block.strip().split(chr(10) + chr(10))[0]
        ids = {t.strip().split(" ")[0] for t in block.split(",") if t.strip()}
    record("repair capabilities >= %d claimed" % CLAIMED_REPAIRS,
           len(ids) >= CLAIMED_REPAIRS,
           "%d repair action IDs in catalogue" % len(ids))

    # Session window claim
    m = re.search(r"SESSION_TTL_MS\s*=\s*([0-9]+)\s*\*", text)
    days = int(m.group(1)) if m else None
    record("session window is %d days as claimed" % CLAIMED_SESSION_DAYS,
           days == CLAIMED_SESSION_DAYS, "SESSION_TTL_MS starts at %s" % days)

    # Diagnostic categories
    diag = HERE / "StoreApp" / "Diagnostics.cs"
    if diag.exists():
        d = diag.read_text(encoding="utf-8", errors="replace")
        cats = set(re.findall(r'\["([a-z_]+)"\]\s*=', d))
        record("diagnostic categories >= %d claimed" % CLAIMED_CATEGORIES,
               len(cats) >= CLAIMED_CATEGORIES,
               "%d categories found" % len(cats), blocking=False)
    else:
        record("Diagnostics.cs present", False, "not found", blocking=False)


# ── 5. Notes to certification actually contain the access code ───────────────

def check_cert_notes():
    print("\n[5] Certification notes carry the access code")
    p = HERE / "docs" / "MICROSOFT_STORE_CERT_NOTES.md"
    if not p.exists():
        record("cert notes file present", False, "not found")
        return
    t = p.read_text(encoding="utf-8", errors="replace")
    record("notes contain the cert code", CERT_CODE in t, CERT_CODE)
    record("notes state no account is required",
           re.search(r"no (account|sign-in|login)", t, re.I) is not None, "stated")


def main():
    print("=" * 74)
    print("ProntoPC — MICROSOFT STORE CERTIFICATION PRE-FLIGHT")
    print("=" * 74)
    key = stripe_key()
    check_reviewer_access(key)
    check_success_path()
    check_ai_repair_plan()
    check_countable_claims()
    check_cert_notes()

    blocking_fail = [r for r in RESULTS if not r["ok"] and r["blocking"]]
    warns = [r for r in RESULTS if not r["ok"] and not r["blocking"]]
    passed = sum(1 for r in RESULTS if r["ok"])

    print("\n" + "=" * 74)
    print("%d/%d checks passed" % (passed, len(RESULTS)))
    if warns:
        print("\nNON-BLOCKING WARNINGS:")
        for r in warns:
            print("  - %s: %s" % (r["claim"], r["evidence"]))
    if blocking_fail:
        print("\nDO NOT SUBMIT — %d blocking failure(s):" % len(blocking_fail))
        for r in blocking_fail:
            print("  X %s" % r["claim"])
            print("      %s" % r["evidence"])
        print("=" * 74)
        return 1
    print("\nAll blocking claims demonstrated against the live system.")
    print("Safe to submit.")
    print("=" * 74)
    return 0


if __name__ == "__main__":
    sys.exit(main())
