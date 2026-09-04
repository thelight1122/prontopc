# ProntoPC — Store Submission Evidence Sheet

**Internal pre-submission record.** Not the reviewer-facing notes — those are
[`MICROSOFT_STORE_CERT_NOTES.md`](MICROSOFT_STORE_CERT_NOTES.md), pasted into Partner Center
"Notes for certification." This sheet is the evidence that the release candidate passed SI-side
verification before submission, tied to the exact tree state it attests to.

| Field | Value |
| --- | --- |
| Product | ProntoPC |
| Store / Product ID | `9P1K9QJXKND8` |
| Package version | `1.2.0.7` |
| Verified at | 2026-09-04 (UTC), source Totem **TOTEM-C-0165** (WACK-polish rebuild) |
| Unsigned submission artifact | `StoreApp/ProntoPC.msix` — copy `release-candidates/ProntoPC_1.2.0.7_x64_unsigned_partnercenter.msix` |
| SHA-256 | `1D759844A72B6C6991B03E2BE219D1571833F8B4F154F069B88CFFC7D3F5AD0B` (735.5 MB, **unsigned** — Microsoft signs) |
| Package architecture | `x64` (was `neutral`) |
| Re-verify with | `python pod/totems/verify_totems.py` · `python certification_preflight.py` (v2) |

---

## 1. Verification results (SI-side, v1.2.0.6 pass — 2026-09-04)

Run with the **v2** preflight, which now executes ModelSmoke and inspects the real package. Every
machine-checkable gate passes; the only non-green item is **WACK**, which requires an elevated shell
(a Tracey step), so the tool's own verdict reads **DO_NOT_SUBMIT until WACK is run** — by design.

| Check | Result | Evidence |
| --- | --- | --- |
| Certification preflight (v2) | **all machine-checkable gates PASS**; `DO_NOT_SUBMIT` pending WACK only | `python certification_preflight.py`; evidence `docs/CERTIFICATION_PREFLIGHT_EVIDENCE.json` |
| Reviewer $0 access (`MSCERT26`) | **PASS** — active, 100% off, 25 redemptions left, not expired | preflight `[1]` — promo `promo_1UBka3LLKFLplB2Zc6pcidVH` on coupon `8kRBfcok` |
| Post-checkout path | **PASS** — `prontopc.online` 200, `/success` 200, backend responding | preflight `[2]` |
| **Package inspected directly (PKG-001)** | **PASS** — package version `1.2.0.6` == source; model assets **inside** the package | preflight `[6]` |
| — model `genai_config.json` in package | **found** | inside `Assets/models/gemma-3-1b-it-formed-v0.2.1/` |
| — model weights in package | **822 MB** (`model.onnx.data`, int4) | inside the package |
| — formation provenance in package | **found** (`aegis-formation-manifest.json`) | inside the package |
| ModelSmoke (real on-device authoring) | **PASS** — `exit 0`, `MODEL SMOKE PASS` (~20s) | preflight `[3b]` executes `dotnet run --project tests/ModelSmoke -c Release` |
| Listing claims (all blocking) | **PASS** — repairs 26 ≥ 18 · session 3 days · categories 95 ≥ 35 | preflight `[4]` |
| Certification notes match flow | **PASS** — no account, on-device AI plan, `MSCERT26`, UAC→scan→Perform Repairs | preflight `[5]` |
| Backend live | health **200**, `request-refund` fail-closed | Cloud Run rev `pronto-pc-00083-qtn` |
| **WACK** (on v1.2.0.6) | **WARNING, 0 blocking FAILs — submittable** | 21 PASS / 2 optional FAIL / 1 WARNING; overall WARNING (optional-test fails don't block) |
| **WACK re-run on v1.2.0.7** | **PENDING — manual, elevated** | v1.2.0.7 applies the two polish fixes below; re-run `appcert.exe` elevated to confirm a clean PASS |

**WACK polish (v1.2.0.7):** the v1.2.0.6 WACK WARNING came from two *optional* fails + one warning, all now
addressed: (1) *Platform appropriate files* (bundled x64 runtime vs a `neutral` package) → package
`ProcessorArchitecture` set to **x64**; (2) *DPI awareness* (blurry on high-DPI) → **PerMonitorV2** declared via an
embedded `app.manifest`. The third flag, *Blocked executables* (the app references `ShellExecute`/`CreateProcess` to
open the checkout URL and the report), is a benign optional check for a full-trust desktop app and does not block.
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

A Microsoft "PASS" is issued **after** submission and is not producible SI-side. Status as of 2026-09-04:

1. **Backend deployed** ✅ — `pronto-server-cloudrun.mjs` live at Cloud Run rev `pronto-pc-00083-qtn`
   (TOTEM-C-0160); `/api/request-refund` + refund-XOR-coupon active and fail-closed.
2. **Stripe LIVE confirmed** ✅ — `MSCERT26` 100%-off active (25 redemptions), and the `$29` Payment
   Link redirects to `success-store?session={CHECKOUT_SESSION_ID}` (the Store continuation page — **not**
   the legacy `success.html`).
3. **Unsigned submission artifact built** ✅ — `StoreApp/ProntoPC.msix` **v1.2.0.7**, sha `1D759844…`,
   the formed model bundled, identity `5DServiceSolutionsGlobal.ProntoPC` / Store `9P1K9QJXKND8`, arch `x64`.
   Microsoft signs at submission. *(A literal `.wapproj` `.msixupload` container needs the VS "Windows
   application packaging" component; the unsigned `.msix` is `build.ps1`'s documented Partner Center upload.)*
4. **Controlled installed-package E2E** — partly done: v1.2.0.6 installed and run on the test machine
   (grounded on-device Technician reply, Start/Skip Repair consent). Remaining owner steps:
   **run WACK from an elevated shell** (`appcert.exe test -appxpackagepath StoreApp\ProntoPC.msix
   -reportoutputpath wack-report.xml`) and a full paid-$0 → scan → Perform Repairs pass.
5. **Submit in Partner Center** with `MICROSOFT_STORE_CERT_NOTES.md` in "Notes for certification."
6. **On cert PASS** — promote `prontopc-repair-plan` provisional → verified (a Totem).

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

**Remaining external gates:** controlled installed-package E2E and WACK, Store signing/upload
workflow, and Partner Center submission.
