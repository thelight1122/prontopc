## Current candidate — 2026-10-02: v1.2.21.0 AI reporting / AEGIS disclosure

Supersedes prior candidate readiness claims below. Microsoft’s October 2 report
cites 11.16 Live Generative AI Content: missing reporting for inappropriate AI output.
This is a policy-coverage shortfall; the report does not attest to all other areas passing.

- Unsigned upload: `H:\PRONTOPC_PRODUCTION\StoreApp\ProntoPC.msixupload`
- Upload SHA-256: `8D2FF6B1867FE22BE1612A239B906F80E931EBD9BBB6ED48993DC1B36CFD82C3`
- Inner unsigned MSIX SHA-256: `228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655`
- Version: `1.2.21.0`, x64; upload contains byte-identical `ProntoPC_1.2.21.0_x64.msix`.
- Build and publish: PASS. Earlier 1.2.20 packages preserved in release-candidates.
- Report preparation/encoding checks: 6/6 PASS; actual WPF report UI checks PASS
  (editable selected reply, empty content prevention, redaction reflected, small viewport scroll).
- Package inspection: PASS; reporting dialog/helper and plan/reply/AEGIS handlers
  present in ProntoPC.dll; no AppxSignature.p7x in the Store package.
- Public explanation: https://prontopc.online/ai-aegis deployed and verified HTTP 200;
  homepage links present, privacy/terms/emblem remain HTTP 200.
- Vercel deployment: `dpl_AK6gjdCzBNV8MKNzCCFkg1m2iQWR` (production alias prontopc.online).
- WACK: **PASS VERIFIED** — user-run timestamped, canonical and current reports match, APP_VERSION=1.2.21.0, fresh after the final package. Report SHA-256 `A2E4E4B640723550D5993561AFDC6F7402FECA14EC9D88B3A3F443EDE5B97054`. `run-wack-12210.ps1` pins both package hashes and version,
  requires elevation, keeps timestamped reports and promotes only a valid PASS.
- Installed exact-package reporting flow and support mailbox receipt/monitoring: PENDING.
- Refreshed preflight v3.5: **22/23 PASS, DO_NOT_SUBMIT**. Fresh WACK and ModelSmoke PASS; only installed reporting/mailbox verification remains pending.
- Preflight v3.5 adds policy 11.16 reporting instructions/procedure and a separate
  exact-upload-hash-bound manual reporting verification gate. Consult the current JSON
  and final preflight log; no CERT_READY claim until all gates pass.

The user asked Codex to implement/build and provide the WACK script for the user
 to run. No WACK run, host install/signing, Partner Center action, commit, push,
 or trust mutation was performed by Codex. The authorized website explanation was deployed.
 The existing dirty work was preserved. Microsoft approval remains external.

### User-run WACK

Open PowerShell **as Administrator**, close any ProntoPC window, then run:

```powershell
cd 'H:\PRONTOPC_PRODUCTION'
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-wack-12210.ps1
```

Expected: `WACK PASS for ProntoPC 1.2.21.0`, `OVERALL_RESULT: PASS`,
`APP_VERSION: 1.2.21.0`; canonical report `H:\PRONTOPC_PRODUCTION\wack-report-12210.xml`.
On mismatch/failure the script stops and retains the timestamped report without
promoting it. Before resubmission, follow AI_CONTENT_REPORTING_PROCEDURE.md,
verify support delivery and monitoring, and confirm live generative AI in the Store
metadata and Partner Center declarations. The owner completes observed results in
AI_CONTENT_REPORT_VERIFICATION.json; do not set pending flags based on WACK alone.

---

# ProntoPC — Store Submission Evidence Sheet

**Internal pre-submission record.** Not the reviewer-facing notes — those are
[`MICROSOFT_STORE_CERT_NOTES.md`](MICROSOFT_STORE_CERT_NOTES.md), pasted into Partner Center
"Notes for certification." This sheet is the evidence that the release candidate passed SI-side
verification before submission, tied to the exact tree state it attests to.

| Field | Value |
| --- | --- |
| Product | ProntoPC |
| Store / Product ID | `9P1K9QJXKND8` |
| Package version | `1.2.20.0` |
| Verified at | 2026-10-01 (America/Phoenix) — see §0 current status |
| Unsigned submission artifact | `StoreApp/ProntoPC.msixupload` |
| SHA-256 | `F4B56BA2E8DCE0F898AE569A8FA947606D3344B2EE8A865593FE7FE34C272EA3` (734.5 MB, **unsigned Store upload container** — Microsoft signs) |
| Package architecture | `x64` (was `neutral`) |
| Re-verify with | `python pod\totems\verify_totems.py` · `python certification_preflight.py --package H:\PRONTOPC_PRODUCTION\StoreApp\ProntoPC.msixupload` (v3.4) |

---


## 0a. Current resubmission candidate — 2026-10-01 (supersedes v1.2.17.0 CERT_READY)

**Claude handoff C-0189 / Cert VM finding:** the persistent Hyper-V Cert VM did **not** reproduce `0xc0000142` on the tested v1.2.16.0 paths, so the elevated-relaunch diagnosis is not supported by that VM evidence. A new reviewer-facing shortfall was observed instead: the WPF main window was taller than the VM work area, and terminal panels such as Complete/Error/Retry/MakeGood/Refund did not scroll, leaving the bottom button unreachable on smaller or high-scaling displays.

**Current rebuilt candidate: v`1.2.20.0`.** This build carries forward the Cert VM display-fit and plan-authoring diagnostic repairs, and fixes the CertUser repair-path shortfall where the Drive/Disk Cleanup action could sit behind the taskbar until clicked. The `disk_cleanup` action no longer launches Windows Disk Cleanup/`cleanmgr`; ProntoPC clears safe temporary files directly and reports the result, so that repair step has no hidden UI prompt. ModelSmoke, WACK, and certification preflight pass on the exact v1.2.20.0 upload package; the corrected CertUser VM rerun completed end-to-end with no errors and the UI looked right, closing the final reviewer-simulation confidence gate before upload.

| Gate | Result | Evidence |
| --- | --- | --- |
| Package (PKG-001) | **PASS** — v1.2.20.0 == source; fourth version segment is `.0`; model assets and VC runtime DLLs are inside the package | `StoreApp/ProntoPC.msixupload`, sha `F4B56BA2E8DCE0F898AE569A8FA947606D3344B2EE8A865593FE7FE34C272EA3`, 734.5 MB; inner `StoreApp/ProntoPC.msix`, sha `9FD35FE4F5715DDABB3023B759BB2A24DE0805F264A9C089A56BCD54373E1377`, 736 MB |
| Release build/package | **PASS** — `dotnet build -c Release` passed, then `StoreApp/build.ps1` produced unsigned MSIX and MSIXUPLOAD artifacts | local build/package, 2026-10-01 |
| Certification preflight (v3.4) | **CERT_READY — 21/21 PASS** — package inspection, VC runtime coverage, ModelSmoke, reviewer notes, and WACK all pass on v1.2.20.0 | `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json`, generated 2026-10-01 |
| WACK | **PASS** — `OVERALL_RESULT="PASS"`, `APP_VERSION="1.2.20.0"` on exact inner package hash `9FD35FE4F5715DDABB3023B759BB2A24DE0805F264A9C089A56BCD54373E1377`; report hash `7402E141…` | `wack-report-12200.xml` / `wack-report-current.xml` |
| Cert VM reviewer simulation | **PASS** — Tracey reported the corrected VM run completed end-to-end as `CertUser` with no errors and the UI looked right; specifically validates the reviewer-style standard-user flow after the harness fix and Disk Cleanup hidden-UI repair | `tests/CertVM/run-certvm-diagnosis.ps1`, user-observed VM run 2026-10-01 |
## 0. Historical v1.2.17.0 status — 2026-09-30 (superseded by §0a)

**Microsoft certification shortfall received 2026-09-30:** the v`1.2.16.0` submission was rejected
under **10.1.2.10 Functionality** because Microsoft observed `ProntoPC.exe - Application Error`:
the application was unable to start correctly (`0xc0000142`) on OS build `10.0.26200 Build 26200`.
The screenshot shows a process-start failure before the app can display its own WPF error handling.

**Crash-response candidate: v`1.2.17.0`.** ONNX Runtime GenAI imports desktop VC++ runtime DLLs:
`MSVCP140.dll`, `MSVCP140_1.dll`, `VCRUNTIME140.dll`, and `VCRUNTIME140_1.dll`. The rejected
v`1.2.16.0` evidence accepted a manifest `Microsoft.VCLibs.140.00.UWPDesktop` dependency while the
package omitted those DLLs. That was too weak for clean Microsoft reviewer devices. `StoreApp/build.ps1`
now stages the x64 VC143 runtime DLLs beside `ProntoPC.exe`, and `certification_preflight.py` v3.4 now
blocks any ONNX-native package that omits them, even when the VCLibs framework dependency is declared.

**Current state: local pre-submission CERT_READY; isolated VM launch still pending.** The v`1.2.17.0`
package/source version matches, the model is present in the package, WPF resources are compiled, the VC
runtime DLLs are inside the package, the loose rebuilt layout launches locally, ModelSmoke passes real
on-device authoring, and WACK passes on the exact inner MSIX. Because Microsoft rejected v`1.2.16.0` at
process startup, a clean Test VM / reviewer-like launch remains the final confidence gate before
resubmission even though preflight v3.4 now reports `CERT_READY`.

| Gate | Result | Evidence |
| --- | --- | --- |
| Package (PKG-001) | **PASS** — historical v1.2.17.0 exact package, now superseded by §0a | `StoreApp/ProntoPC.msixupload`, sha `C5A59981E78DF32FD748B617BC364D00155C23E2D5B4505FB9CB3173B63FD0D1`; inner `StoreApp/ProntoPC.msix`, sha `317452962A75F03AC2407BCFB9BEA261FFDDC8EFFA31A11614E6CD7A401C865E` |
| Certification preflight (v3.4) | **CERT_READY** — 21/21 checks PASS; package inspection, VC runtime coverage, ModelSmoke, reviewer notes, and WACK all pass | `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json`, generated 2026-09-30 |
| Loose layout launch | **PASS** — rebuilt `StoreApp\package-layout\ProntoPC.exe` stayed running after launch; no local `0xc0000142` reproduction | local launch smoke, 2026-09-30 |
| ModelSmoke (on-device authoring) | **PASS** — `MODEL SMOKE PASS`; reviewer-style clean audit, local-model provenance, admissible action, and grounded 2-turn chat | `dotnet run --project tests\ModelSmoke -c Release`, 2026-09-30 |
| WACK | **PASS** — `OVERALL_RESULT="PASS"`, `APP_VERSION="1.2.17.0"` on exact inner package hash `31745296…`; report hash `80F56095…` | `wack-report-12170.xml` / `wack-report-current.xml` |
| Clean Sandbox / reviewer-like launch | **PENDING** — Test VM is still installing; must be run against v1.2.17.0 because v1.2.16.0 was rejected at process startup | install/run the new candidate on a clean Sandbox or clean VM |
| Reviewer-facing notes | **PASS / UPDATE REQUIRED** — one-time `$9.99` via Microsoft Store, no account, offline, on-device AI; package version updated to v1.2.17.0 | `docs/CERT_NOTES_SHORT.txt` |

**Candidate rebuilt after the first-model-load UX shortfall: v1.2.15.4 carries the version-repaired v1.2.15.0 package, the startup-render fix from v1.2.15.1, the native splash/resource repair from v1.2.15.3, and a below-normal-priority model warm-up during scan so the first plan step is less likely to starve Custodian animation. v1.2.15.0, v1.2.15.1, v1.2.15.2, and v1.2.15.3 are now superseded and must not be submitted.** This is the one-time-paid **Microsoft Store** build. The
Store-app pivot (TOTEM-X-0117..X-0121) retired the Cloud Run backend / Stripe / website-first checkout
to `archive/cloudrun-backend/`; the installed app now runs scan, plan, repair, and report **entirely
on-device**. The Stripe / `$29` / Cloud Run / `MSCERT26` references in §1, §1a–c and §4 below are
**pre-pivot history** and are being reconciled by the packaging/cert (Codex) lane — they do **not**
describe the current one-time-paid Store submission (price `$9.99`, no account, offline).

Historical v`1.2.16.0` note: that candidate fixed Partner Center ingestion by moving to
`.msixupload` and a Store-valid fourth version segment, and it had local WACK/Sandbox evidence before
submission. Microsoft then rejected it on 2026-09-30 for the startup `0xc0000142` failure, so its
`CERT_READY` status is superseded and must not be used for resubmission.

| Delta | What changed | Evidence |
| --- | --- | --- |
| 1.2.9.0 → 1.2.10.0 delta | on-device model-load reliability fix — `OnnxGenAiInference`: permanent load-fail flag → 2s cooldown + `GC.Collect` retry | TOTEM-X-0123 |
| 1.2.10.0 → 1.2.11.0 delta | packages the multi-drive plan-authoring fix — compact drive report, actions-first schema/prompt, short summary cap | TOTEM-C-0185 / TOTEM-C-0186 |
| 1.2.11.0 → 1.2.12.0 delta | waits through the local model-load cooldown before retrying plan authoring, preventing rapid retries from reusing the same transient first-load failure | TOTEM-X-0127 / TOTEM-X-0128 |
| 1.2.12.0 → 1.2.13.0 delta | declares `Microsoft.VCLibs.140.00.UWPDesktop` `MinVersion=14.0.33728.0`, covering ONNX Runtime's clean-machine VC++ runtime imports | TOTEM-X-0130 |
| 1.2.13.0 → 1.2.14.0 delta | integrates the reviewed findings UX hardening: deterministic finding catalog rows, live observe-only detail, clearer success/needs-you language, no-dead-end retry/report fallback, and Tracey's attempt-language copy addendum | TOTEM-X-0132 / TOTEM-X-0133 |
| 1.2.14.0 cert-ready hash → robot-icon hash | replaces the old P tile assets with the thumbs-up robot icon, archives the old P assets, and hides/skips the Store-cache reset when the Microsoft Store protocol is absent so clean Sandbox does not show a surprise Store-link dialog | Verus `e2d2631`, Codex applied locally after TOTEM-X-0134 |
| robot-icon hash → final Sandbox illumination hash | adds unattended Disk Cleanup profile setup, grounds Technician answers so repair titles are not treated as measured findings/severity, and changes Prism to preserve answer content while appending optional reflection instead of replacing the answer | Verus `d2bffdd` + Codex governance fix, WACK PASS / CERT_READY on superseded hash `0C47F505…` |
| final Sandbox illumination hash → final copy-only hash | changes remaining "Start Repair" UI/help text to "Perform Repairs" to match the actual repair-action button | Verus `aa73844` mirrored locally; WACK pending on new hash `8ADAA0DA…` |
| final copy-only hash → update-check hash | adds a launch-time Microsoft Store update check and in-app update banner/button using Store-managed package update APIs | Codex local update-check fix; WACK pending on new hash `8591FEDC…` |
| update-check hash → repair-timeout hash | enforces repair-tool timeouts before output reads can block, drains stdout/stderr asynchronously, kills hung process trees, reports timeout honestly, and removes the Disk Cleanup Windows Error Reporting category that stalled Sandbox | Verus Sandbox illumination mirrored locally; WACK PASS + clean Sandbox PASS on hash `E192F0BD…` |
| repair-timeout hash → version-repair hash | bumps the Store package identity from already-uploaded `1.2.14.0` to `1.2.15.0`, rebuilds the unsigned Partner Center MSIX, and refreshes preflight so package/source version match | Codex version repair; WACK pending on new hash `FAD6FB49…` |
| version-repair hash → startup-render revision | bumps the Store package identity to `1.2.15.1` and defers the informational Store update check until after first paint so Sandbox/reviewer launch shows the visible scan state instead of a blank white window | Verus Sandbox shortfall report `F10CD6B6…`; Codex startup-render repair; WACK/Sandbox pending on new hash `6E7E5245…` |
| startup-render revision → native splash revision | bumps the Store package identity to `1.2.15.3`, adds WPF native splash support, cleans stale WPF intermediates before publish, and blocks missing compiled WPF resources in preflight after v1.2.15.2 proved the crash mode | Verus Sandbox crash report `05FA0596…`; Codex clean-build/resource-preflight repair; WACK/Sandbox pending on new hash `B34FB1BD…` |
| native splash revision → model warm-up revision | bumps the Store package identity to `1.2.15.4`, starts model warm-up during scan on a dedicated below-normal-priority background thread, and reuses that warmed inference instance for plan authoring | Verus WACK PASS + Sandbox UX shortfall report `A6D7217C…`; Codex warm-up repair; WACK/Sandbox pending on new hash `62CB3283…` |
| v1.2.16.0 rejection → v1.2.17.0 crash-response candidate | packages the VC143 runtime DLLs required by ONNX Runtime GenAI and upgrades preflight so VCLibs declaration alone no longer passes the clean-machine dependency gate | `StoreApp/build.ps1`; `certification_preflight.py` v3.4; v1.2.17.0 package hash `C5A59981…`; WACK PASS on `wack-report-12170.xml` |

---

## 1. Verification results — PRE-PIVOT HISTORY (superseded by §0; describes the retired Stripe/backend flow)

Run with the **v2** preflight, which now executes ModelSmoke and inspects the real package. Every
machine-checkable gate passes on **v1.2.9.0**, and WACK now returns **OVERALL_RESULT="PASS"** on the
exact v1.2.9.0 package.

| Check | Result | Evidence |
| --- | --- | --- |
| Certification preflight (v2) | **32/33 PASS**; all machine-checkable gates PASS, WACK recorded separately | `python certification_preflight.py --package StoreApp\ProntoPC.msix`; evidence `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json` |
| Reviewer $0 access (`MSCERT26`) | **PASS** — active, 100% off, 18 redemptions left, not expired | preflight `[1]` — promo `promo_1UBmWwLLKFLplB2ZSSIyzRYw` on coupon `8kRBfcok` |
| Website-first post-checkout path | **PASS** — `prontopc.online` 200, `success-store` 200, backend responding | preflight `[2]` |
| **Package inspected directly (PKG-001)** | **PASS** — package version `1.2.9.0` == source; model assets **inside** the package | preflight `[6]` |
| — model `genai_config.json` in package | **found** | inside `Assets/models/gemma-3-1b-it-formed-v0.2.1/` |
| — model weights in package | **822 MB** (`model.onnx.data`, int4) | inside the package |
| — formation provenance in package | **found** (`aegis-formation-manifest.json`) | inside the package |
| ModelSmoke (real on-device authoring) | **PASS** — `exit 0`, `MODEL SMOKE PASS` (~16s) | preflight `[3b]` executes `dotnet run --project tests/ModelSmoke -c Release` |
| Listing claims (all blocking) | **PASS** — repairs 26 ≥ 18 · session 3 days · categories 95 ≥ 35 | preflight `[4]` |
| Certification notes match flow + 10.8.4 disclosure | **PASS** — app-first checkout gate, website-first customer route, reviewer code `MSCERT26`, paid success page, `prontopc://run?token`, no launch UAC, UAC only at **Perform Repairs**, purchase type, price range, Store PDP metadata disclosure | preflight `[5]` |
| Third-party purchase declaration | **OWNER ACTION REQUIRED IN PARTNER CENTER** — select "This app allows users to make purchases, but does not use the Microsoft Store commerce system." | Microsoft rejection 10.8.2 on 2026-09-11 |
| Store metadata purchase disclosure | **OWNER ACTION REQUIRED IN PARTNER CENTER** — add Product Description Page / listing text disclosing the only in-product purchase type and price range: one diagnostic + guided repair run, `$29` per run, no subscription/add-ons/trial/randomized purchases | Microsoft rejection 10.8.4 on 2026-09-14 |
| Backend live | health **200**, `request-refund` fail-closed | Cloud Run rev `pronto-pc-00083-qtn` |
| **WACK on v1.2.9.0** | **PASS** — `OVERALL_RESULT="PASS"` | `wack-report-1290.xml` / `wack-report-current.xml`; `APP_VERSION="1.2.9.0"`, generated 2026-09-09 7:39:47 PM |

## 1a. 2026-09-09 Microsoft rejection response

Microsoft rejected the 2026-09-09 submission under **10.1.2.10 Functionality** because the product
crashed at launch on a Dell Inspiron 14 running OS build `26200.8457`. The v1.2.9.0 candidate changes
the startup behavior directly:

- Removed mandatory self-elevation from `App.OnStartup`; Store launch now opens the normal WPF window
  and shows the payment gate without a UAC prompt.
- Deferred administrator approval until the user explicitly clicks **Perform Repairs**.
- Added startup exception logging to `%LOCALAPPDATA%\ProntoPC\logs\startup-crash.log` so a future
  launch fault produces support evidence instead of only a generic application error.
- Updated reviewer notes and preflight checks so the submitted instructions match the new flow.

## 1b. 2026-09-11 Microsoft rejection response

Microsoft rejected the current submission under:

- **10.3.3 App Is Testable** — content was not accessible. The tester also identified the purchase
  point at **Start your diagnostic**, which means certification may begin from the Store/app-first
  path rather than the website-first customer path.
- **10.8.2 Third-Party In-Product Purchases** — the product offers a paid diagnostic + repair run
  through Stripe and must declare that it allows purchases outside the Microsoft Store commerce
  system.

Corrective response:

- `docs/CERT_NOTES_SHORT.txt` and `docs/MICROSOFT_STORE_CERT_NOTES.md` now lead with reviewer
  access and include the app-first route: launch the submitted app, click **Start your $29
  diagnostic**, apply `MSCERT26`, keep the app open on **Waiting for your payment**, and it
  continues automatically after the $0.00 Stripe checkout.
- The notes also keep the website-first customer route for the live path at `https://prontopc.online`.
- The notes now explicitly state the required Product declaration wording:
  "This app allows users to make purchases, but does not use the Microsoft Store commerce system."
- Separate owner action remains required in Partner Center: go to **Properties -> Product
  declarations** and select that checkbox before resubmitting.

## 1c. 2026-09-14 Microsoft rejection response

Microsoft rejected the latest submission under **10.8.4 Financial Transactions - Purchase Types
and Prices** because information about in-product purchase types and price range was missing or
inaccurate.

Corrective response:

- `StoreApp/MainWindow.xaml` now tells the user before checkout: purchase type is one ProntoPC
  diagnostic + guided repair run for this PC; price range is `$29` per run; no subscription or
  add-ons.
- The app's checkout button now says **Start $29 Stripe checkout**, and the checkout helper text
  states that the user is starting a paid purchase through Stripe.
- `docs/CERT_NOTES_SHORT.txt` and `docs/MICROSOFT_STORE_CERT_NOTES.md` now include explicit
  10.8.4 language: purchase type, price range, no subscription, no add-on tier, no trial, and no
  randomized purchase.
- The notes now include exact Store listing / PDP metadata text to paste in Partner Center:
  "In-product purchases: ProntoPC offers one purchase type, a one-time diagnostic + guided repair
  run for this PC. Price range: $29 per run. No subscription, no add-ons, no trial, and no
  randomized purchases. Payment is processed securely by Stripe."
- Separate owner action remains required in Partner Center: update the Store Product Description
  Page / listing metadata before resubmitting. If the updated app UI is intended to be part of the
  submission evidence, rebuild and upload a new package; otherwise the metadata correction is the
  immediate 10.8.4 response for the already-submitted package.

**WACK history:** v1.2.0.6 read **WARNING** (0 blocking fails — already submittable) from two *optional* fails + one
warning. v1.2.0.7 addressed them: (1) *Platform appropriate files* → package `ProcessorArchitecture` = **x64** (now
PASS); (2) *DPI awareness* → **PerMonitorV2** via embedded `app.manifest` (now PASS). The remaining single *optional*
FAIL, *Blocked executables* (the app references `ShellExecute`/`CreateProcess` to open the checkout URL and the report),
is benign for a full-trust desktop app — **OVERALL_RESULT is PASS**, so it does not block certification.
| Governance gate | **`enabled:true`** — §8 post-scan Technician active (TOTEM-C-0144) | live governed chat, no canned path (C-0148) |

## 2. What the reviewer exercises (and what they do not)

- **On-device AI repair plan** — authored locally by the bundled int4 `gemma-3-1b-it` (no network
  dependency). This is the "AI-powered repair plan" claim, proven present by ModelSmoke.
- **Custodian actuator** — an on-screen vector figure that reflects the current step; a visual status
  indicator only, generates no text.
- **Conversational Technician** — active on the post-scan Findings screen. Every inbound turn is
  PIM-screened; every model reply passes the integrity/RBC/PRISM boundary and is recorded in the
  hash-chained interaction ledger. It is grounded in the session findings and cannot execute or
  authorize repair; **Start Repair** remains the explicit consent path.

## 3. Backing Totems (auditable lineage)

| Totem | What it records |
| --- | --- |
| TOTEM-C-0042 | Cert-done roadmap (owner-tagged) |
| TOTEM-C-0043 | Cert notes refreshed to match the shipped UI (actuator + gated Technician) |
| TOTEM-C-0044 | Multi-turn echo fixed; 2-turn on-device exchange re-proven |
| TOTEM-C-0045 | AEGIS governance status reconciled; Cloud Run persistence gap flagged |
| TOTEM-C-0046 | `await-payment` made stateless via Stripe lookup (committed) |
| TOTEM-C-0048 | This verification pass (release candidate green) |

## 4. Remaining steps to submit — external / owner: Tracey

A Microsoft "PASS" is issued **after** submission and is not producible SI-side. Status as of 2026-09-07:

1. **Backend deployed** ✅ — `pronto-server-cloudrun.mjs` live at Cloud Run rev `pronto-pc-00083-qtn`
   (TOTEM-C-0160); `/api/request-refund` + refund-XOR-coupon active and fail-closed.
2. **Stripe LIVE confirmed** ✅ — `MSCERT26` 100%-off active (22 redemptions), and the `$29` Payment
   Link redirects to `success-store?session={CHECKOUT_SESSION_ID}` (the Store continuation page — **not**
   the legacy `success.html`).
3. **Unsigned submission artifact built** ✅ — `StoreApp/ProntoPC.msix` **v1.2.9.0**, sha `3502B5D4…`,
   the formed model bundled, identity `5DServiceSolutionsGlobal.ProntoPC` / Store `9P1K9QJXKND8`, arch `x64`.
   Microsoft signs at submission. *(A literal `.wapproj` `.msixupload` container needs the VS "Windows
   application packaging" component; the unsigned `.msix` is `build.ps1`'s documented Partner Center upload.)*
4. **WACK** ✅ — v1.2.9.0 passed with `OVERALL_RESULT="PASS"` in `wack-report-1290.xml`.
5. **Partner Center draft needs update** ⏳ — paste the revised `docs/CERT_NOTES_SHORT.txt` so it
   includes app-first reviewer route, website-first customer route, purchase type, and price range.
6. **Product declaration needs update** ⏳ — in Partner Center **Properties -> Product declarations**,
   select: "This app allows users to make purchases, but does not use the Microsoft Store commerce
   system."
7. **Store listing metadata needs update** ⏳ — add the 10.8.4 PDP disclosure: one purchase type,
   one diagnostic + guided repair run, `$29` per run, no subscription/add-ons/trial/randomized purchases,
   Stripe checkout.
8. **Fresh screenshots** ⏳ — capture updated UI/listing images from the current app/web flow; do not reuse
   screenshots showing old rejected wording.
9. **Package choice gate** ⏳ — if the source UI disclosure should be included in the submitted binary,
   rebuild and upload a new package; if the immediate correction is Store metadata only, keep the current
   package and update Partner Center metadata/notes.
10. **Final external gate: Resubmit for certification** — the button is available in Partner Center, but
   requires separate owner confirmation at action time.
11. **On cert PASS** — promote `prontopc-repair-plan` provisional → verified (a Totem).

*Current governance boundary for cert: `PipelineGovernance` is **enabled** for the governed, post-scan
Technician (TOTEM-C-0144). It remains on-device, findings-grounded, and non-executing; explicit consent
and the admissible-action envelope remain the only repair path.*

---

## Recert rebuild pass — 2026-09-01 (TOTEM-C-0097)

Fresh recert package rebuilt from current HEAD so it carries every post-`X-0055`-freeze fix — most
importantly **X-0061** (greedy `do_sample:false` on the plan path = the make-good fix), plus the
**C-0071** System Audit gate, **C-0060** completeness backstop, and **C-0074/-0076** in-model Canon.

| Field | Value |
| --- | --- |
| Package version | `1.2.0.2` (unsigned MSIX — Partner Center signs) |
| Uploadable file | `StoreApp/ProntoPC.msix` (archived: `release-candidates/ProntoPC_1.2.0.2_x64_recert_24198b52.msix`) |
| SHA-256 | `24198B52919AF474394C6FA533C8F0C3E7B52B800C9FF363013C70C017CC3175` |
| Size | 648.7 MB |
| Built from | `main` @ `fec8b1a` (TOTEM-C-0096) + packaging fixes (this totem) |
| Identity | `5DServiceSolutionsGlobal.ProntoPC`, Publisher CN `D4266722-…`, EntryPoint `Windows.FullTrustApplication` |

**Content-verified (SI-side):** exactly one model shipped — `gemma-3-1b-it` (9 files, `model.onnx.data`
693.83 MB); **0 `.pdb`**; both license notices (`GEMMA-NOTICE.txt`, `THIRD-PARTY-NOTICES.txt`);
version/identity/entrypoint correct. **Packaging fix this pass:** `ProntoPC.csproj` model include
scoped to `gemma-3-1b-it`, and `build.ps1` now prunes any non-shipped model from the layout — so the
IRG's **analyzer research model** (`gemma-3-1b-analyzer`, gate `enabled:false`, not in the product
flow) does **not** leak into the package (caught a 1.29 GB leak; corrected to 648.7 MB).

**Preflight:** `certification_preflight.py` → **18/19, "Safe to submit."** Sole warning is the cloud
Gemini **429** (credits depleted) — **non-blocking**: the on-device model authors the plan; cloud is
fallback-only, and its absence routes to the honest make-good, never a non-AI substitute.

**REMAINING GATE (Tracey's, external):** the one thing not provable SI-side — a **controlled paid
install + E2E run** on the freshly-installed package confirming the on-device Custodian **authors a
plan locally** (`provenance=local-model`), NOT `ShowMakeGood` (the exact real-run failure X-0061
targets, which has only ever been verified in source). Then: Partner Center submit (this MSIX +
`MICROSOFT_STORE_CERT_NOTES.md`), and the web deploy backlog (C-0073/C-0085 + confirm the $0-checkout
redirect fix is live). At this historical recert snapshot, the governance gate stayed `enabled:false`.

---

## Formed-model candidate rebuild — 2026-09-03 (TOTEM-X-0086)

This successor replaces the historical base-model package above as the current **unsigned
certification candidate**. It selects the Canon-formed CPU responder
`gemma-3-1b-it-formed-v0.2.1`; its formation manifest binds the frozen base, responder adapter,
locked Canon fingerprint, and the bounded Product Nature Layer. The Nature Layer supplies only
evidence-domain context alongside the validated SystemAudit. It never supplies facts or execution
authority. At this formed-model candidate snapshot, the peer-facing conversational governance gate
remained `enabled:false`; the current active state is recorded in the addendum below.

| Field | Value |
| --- | --- |
| Package version | `1.2.0.2` (unsigned MSIX — Partner Center signs a submission) |
| Uploadable candidate | `StoreApp/ProntoPC.msix` |
| SHA-256 | `5DB9462B0B6F2DBCA1997820057C511068C61F85C04047F005B94A1A0B0E1D2D` |
| Bundled responder | `gemma-3-1b-it-formed-v0.2.1` CPU INT4 ONNX |
| Model inventory | exactly 10 formed-responder files plus `Assets/aegis/repair-nature-layer.json`; no base or analyzer payload |

**Fresh source evidence:** clean Release build succeeded with 0 warnings/errors. `ModelSmoke` ran
against the exact formed responder, validated a complete 25/25 SystemAudit, a local-model authored
plan with envelope-admissible actions and diagnostic-grounded summary, formation provenance, the
packaged Product Nature Layer, and two on-device Custodian turns.

**SEED evidence:** the formal SEED is issued for this certification candidate after the acceptance
suite passed against the exact extracted MSIX payload and verified its model, configuration, Nature
Layer, and formation-manifest hashes. This is Product provenance evidence, not a Microsoft approval.

**Current-state addendum — active conversational governance (2026-09-03):** the later v1.2.0.4
candidate supersedes the earlier closed-gate snapshot above. The post-scan Technician is active:
PIM screens inbound turns; integrity/RBC/PRISM shapes outbound replies; the append-only interaction
ledger records completed turns. The embedded Canon-formed local model explains session findings but
never executes a repair. The controlled installed-package E2E will exercise a live grounded question
before the separate Start Repair / Skip Repair consent choice.

**Remaining external gates:** controlled installed-package E2E if owner elects, Store signing/upload
workflow, and Partner Center submission.




## Observation-only gate refresh — 2026-10-03

The 1.2.21.0 candidate at the top of this document is now ARCHIVED, following
TOTEM-X-0176/X-0177 baseline restoration. Active StoreApp upload is 1.2.20.0,
SHA-256 F4B56BA2E8DCE0F898AE569A8FA947606D3344B2EE8A865593FE7FE34C272EA3.
Archived reporting upload remains at release-candidates/disclaimer-enhancements-20261002/ProntoPC.msixupload,
SHA-256 8D2FF6B1867FE22BE1612A239B906F80E931EBD9BBB6ED48993DC1B36CFD82C3;
its unsigned MSIX hash remains 228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655.
Both archived hashes and active upload hash freshly verified. Current host account
has 1.2.10.0 installed. No fresh guest installed identity/reporting or mailbox
receipt/monitoring result was observed. Existing reporting verification flags remain false.

Current restored preflight v3.4 against the archived upload: 19/21 DO_NOT_SUBMIT,
source/package mismatch 1.2.20.0 versus 1.2.21.0; WACK manual unresolved by this
runner. ModelSmoke PASS against current source, not archived installed acceptance.
This runner lacks the v3.5 reporting gate; do not treat its output as complete
candidate acceptance. Historical 1.2.21 WACK PASS remains separate evidence.
Fresh snapshot: docs/CERTIFICATION_PREFLIGHT_12210_RECHECK_20261003.json.
Exact owner steps and result schema: docs/CERT_GATE_12210_OBSERVATION_CHECKLIST_20261003.md.
Prior installed model_not_loaded remains unresolved. No submission, signing,
installation, trust mutation, VM write, mailbox message, commit or push performed.
