# ProntoPC Certification Preflight v2 — Completion Report (reply to Lumin)

**Re:** `ProntoPC_Certification_Preflight_v2_Work_Request.json`
**Target:** `certification_preflight.py` (evolved in place — not rewritten from scratch, per your "do_not_replace")
**Status of this work:** implemented + validated on live systems. The preflight itself now returns a machine-readable verdict; it does **not** declare CERT_READY just because the script ran.

First — thank you. This was a sharp review and most of it landed. One finding (ARCH-001) rested on an assumption about the product that isn't true, and implementing it as written would have *removed* the gates that catch two of our historical rejections — so I corrected the root cause of that misread instead. Details below, finding by finding.

---

## The one I did NOT implement as written — ARCH-001 (and why)

**Your read:** the shipping Store app is "self-contained… no promo code, no website, no separate download," so the Stripe / prontopc.online / Cloud Run checks are legacy and should stop blocking Store submission.

**What's actually true (verified in source):** only the **AI repair-plan authoring** is on-device (bundled Gemma, no cloud fallback — retired 2026-09-02). The **product is paid**: each run is unlocked by a one-time **$29 Stripe checkout** in the browser (`CheckoutLink = buy.stripe.com/…`, `GateBuy_Click`), the reviewer returns via **prontopc.online**, and the app calls the **Cloud Run backend** for payment-verify, consent, repair-start, the report, refunds, and make-good (`MainWindow.xaml.cs`). The cert notes themselves walk the reviewer through "*browser opens the secure Stripe checkout*" + the 100%-off code.

So sections 1–2 are **the shipping reviewer path**, not legacy — they are exactly the gates that catch **10.3.3** (reviewer no-cost access) and **10.1.2.10** (post-checkout path resolves). Removing them would re-open both.

**What led to the misread:** an inaccurate comment in the old script (section 5) literally said *"The on-device app is self-contained: no promo code, no website, no separate download."* That was wrong and I've **corrected it in the code**, and relabeled the section-5 check from "on-device AI with no network dependency" to "on-device AI **plan** (no network needed for the plan)" so the on-device-AI fact isn't confusable with "no website." I also did **not** add the `store` vs `legacy-web` mode split, because it only makes sense under ARCH-001; the web/Stripe/Cloud-Run gates belong to the `store` product.

---

## Findings map

| Your finding | Disposition |
| --- | --- |
| **AI-001** — ModelSmoke not actually run | **Implemented.** `run_model_smoke()` now executes `dotnet run --project tests/ModelSmoke -c Release`, captures exit code + duration + stdout, and PASSes only on exit 0 **and** `MODEL SMOKE PASS`. Verified live: exit=0, ~20s, PASS. `--no-smoke` downgrades it to a blocking manual-required (never a silent pass). |
| **PKG-001** — proves source, not the package | **Implemented.** `find_submission_package()` + `_package_entries()` open the real `.msixupload`/`.msix` (descending through the bundle wrapper) and verify the Gemma `genai_config.json`, `model.onnx.data` (>100 MB), and `aegis-formation-manifest.json` are **inside**, plus the package version matches the source manifest, and record path/size/sha256. |
| **CLAIM-001** — 35+ categories non-blocking | **Implemented.** The diagnostic-category gate is now blocking (repair-count and session-window already were). |
| **POLICY-001** — old policy numbers treated as current | **Implemented.** New `[0]` section prints the historical rejection codes labelled **HISTORICAL** with a note to re-check current policy text at submission. |
| **EVIDENCE-001** — PASS on file existence | **Adopted as the governing principle** (drives AI-001 + PKG-001). Where a machine can run the behaviour, it now runs it. |
| Evidence JSON output | **Implemented.** Every run writes `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json` with all your `minimum_fields`; `final_status` ∈ {CERT_READY, DO_NOT_SUBMIT}. |
| WACK | **Implemented as run-or-manual.** appcert.exe is detected; by default it emits a **blocking manual-required** with the exact elevated command; `--wack` attempts an automated run and parses `OVERALL_RESULT`. Never a silent pass. |
| ARCH-001 (remove web/Stripe/Cloud-Run) | **Corrected, not implemented** — see above. |
| Full GUI reviewer-flow / network-isolation automation | **Deferred as manual-required**, not faked. Automating a real paid checkout + UAC + live repairs isn't safe to drive headless; the honest gate is an explicit manual-required, which the framework now supports. |

---

## Completion report (your requested fields)

- **Files changed:** `certification_preflight.py` (evolved). **Added:** `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json` (sample), `docs/CERT_PREFLIGHT_V2_COMPLETION_REPORT.md` (this note).
- **Behaviour changed:** three result states became four (PASS / FAIL / WARN / **MANUAL-required**); MANUAL and blocking FAIL both force `DO_NOT_SUBMIT`. ModelSmoke and the package are now executed/inspected. CLI added: `--mode store`, `--package`, `--no-smoke`, `--wack`, `--evidence-out`.
- **Legacy checks removed or isolated:** **none removed.** The web/Stripe/Cloud-Run checks are the live paid reviewer path and stay blocking; the only "isolation" was fixing the false "self-contained" comment and relabeling section 5.
- **New blocking checks added:** ModelSmoke execution; package version-matches-source; three model-assets-inside-package checks; categories-claim (promoted to blocking); WACK (manual-required).
- **Commands executed:** `python -m py_compile certification_preflight.py`; `python certification_preflight.py --no-smoke`; `--package StoreApp/ProntoPC.msix --no-smoke`; `--package StoreApp/ProntoPC.msix` (full, with ModelSmoke).
- **Test results (proving the gates behave):**
  - Default run → picks the newest `.msixupload` = the **stale 1.2.0.0** → PKG-001 blocks (version 1.2.0.0 ≠ source 1.2.0.6; formed model **absent** inside it) → **DO_NOT_SUBMIT**. (This is the real finding: no fresh Store package exists yet.)
  - `--package` the fresh v1.2.0.6 sideload → PKG-001 **PASSes** (version match; config + 822 MB weights + provenance inside).
  - ModelSmoke executed → exit 0, `MODEL SMOKE PASS`.
- **Remaining manual certification checks:** run **WACK** elevated against the candidate package (`--wack`, or the printed command); build the **unsigned `.msixupload`** from head (the sideload `.msix` is not the Store artifact); a human reviewer-flow pass (paid $0 checkout → scan → Perform Repairs) is a manual-required by design.
- **Assumptions still to validate before submission:** the Store **listing text** still claims 35+ categories / 18 repairs / 3-day window (the gates assume these are current — update `CLAIMED_*` if the listing changed); the current Microsoft policy numbering (we treat 10.3.3 / 10.1.2.10 as historical references, not a guarantee of current text).

---

## Net

Under this v2, **CERT_READY requires executed evidence**: a fresh package containing the formed model, a real ModelSmoke pass, the live reviewer $0-checkout path, and WACK. Today it correctly reads **DO_NOT_SUBMIT** — the machine-checkable claims pass, but the submission `.msixupload` is stale and WACK is unrun. That's the truth we wanted the script to be able to state on its own.
