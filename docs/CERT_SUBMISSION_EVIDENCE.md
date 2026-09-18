# ProntoPC — Store Submission Evidence Sheet

**Internal pre-submission record.** Not the reviewer-facing notes — those are
[`MICROSOFT_STORE_CERT_NOTES.md`](MICROSOFT_STORE_CERT_NOTES.md), pasted into Partner Center
"Notes for certification." This sheet is the evidence that the release candidate passed SI-side
verification before submission, tied to the exact tree state it attests to.

| Field | Value |
| --- | --- |
| Product | ProntoPC |
| Store / Product ID | `9P1K9QJXKND8` |
| Package version | `1.2.10.0` |
| Verified at | 2026-09-18 (UTC) — see §0 current status |
| Unsigned submission artifact | `StoreApp/ProntoPC.msix` |
| SHA-256 | `71CA9235E92D68460D76DCACAEC75DCBFA9DF1384A61C62A8BFC2CC434182E5B` (771.2 MB, **unsigned** — Microsoft signs) |
| Package architecture | `x64` (was `neutral`) |
| Re-verify with | `python pod/totems/verify_totems.py` · `python certification_preflight.py` (v3) |

---

## 0. Current submission status — 2026-09-18 (supersedes the version + payment details in §1 and §4)

**Candidate cleared to resubmit: v1.2.10.0.** This is the one-time-paid **Microsoft Store** build. The
Store-app pivot (TOTEM-X-0117..X-0121) retired the Cloud Run backend / Stripe / website-first checkout
to `archive/cloudrun-backend/`; the installed app now runs scan, plan, repair, and report **entirely
on-device**. The Stripe / `$29` / Cloud Run / `MSCERT26` references in §1, §1a–c and §4 below are
**pre-pivot history** and are being reconciled by the packaging/cert (Codex) lane — they do **not**
describe the current one-time-paid Store submission (price `$9.99`, no account, offline).

| Gate | Result | Evidence |
| --- | --- | --- |
| Package (PKG-001) | **PASS** — v1.2.10.0 == source; 822 MB model inside | `StoreApp/ProntoPC.msix`, sha `71CA9235…`, 771.2 MB |
| Certification preflight (v3) | **16/16 machine checks PASS** (WACK + ModelSmoke recorded separately) | `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json` |
| WACK | **PASS** — `OVERALL_RESULT="PASS"`, `APP_VERSION="1.2.10.0"` | `wack-report-1210.xml` (= `wack-report-current.xml`), 2026-09-17 |
| ModelSmoke (on-device authoring) | **PASS** — `MODEL SMOKE PASS`; plan + admissible action + grounded 2-turn chat | `dotnet run --project tests/ModelSmoke -c Release`, 2026-09-18 |
| Reviewer-facing notes | **PASS** — one-time `$9.99` via Microsoft Store, no account, offline, on-device AI | `docs/CERT_NOTES_SHORT.txt` (preflight `[4]`) |
| 1.2.9.0 → 1.2.10.0 delta | on-device model-load reliability fix — `OnnxGenAiInference`: permanent load-fail flag → 2s cooldown + `GC.Collect` retry | TOTEM-X-0123 |

**Upload artifact:** `StoreApp/ProntoPC.msix` (v1.2.10.0, unsigned — Microsoft signs on ingestion). Cleared
to resubmit; remaining owner steps are the Partner Center upload plus the product-declaration / listing
metadata selections (see §4's owner list, adjusted for the one-time-paid Store model).

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
