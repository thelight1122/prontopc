# ProntoPC — Store Submission Evidence Sheet

**Internal pre-submission record.** Not the reviewer-facing notes — those are
[`MICROSOFT_STORE_CERT_NOTES.md`](MICROSOFT_STORE_CERT_NOTES.md), pasted into Partner Center
"Notes for certification." This sheet is the evidence that the release candidate passed SI-side
verification before submission, tied to the exact tree state it attests to.

| Field | Value |
| --- | --- |
| Product | ProntoPC |
| Store / Product ID | `9P1K9QJXKND8` |
| Package version | `1.2.0.0` |
| Verified at | 2026-08-30 (UTC), source Totem **TOTEM-C-0048** |
| Tree state | `main` @ `d2113b5` (the commit carrying TOTEM-C-0048) |
| Re-verify with | `python pod/totems/verify_totems.py` · `python certification_preflight.py` |

---

## 1. Verification results (SI-side, all green this pass)

| Check | Result | Evidence |
| --- | --- | --- |
| Certification preflight | **18/19 — Safe to submit** | `certification_preflight.py`; sole warning is non-blocking (below) |
| StoreApp Release build | **0 warnings / 0 errors** | `dotnet build StoreApp/ProntoPC.csproj -c Release` |
| On-device model bundled | **`model.onnx.data` = 693,829,632 bytes** + `genai_config.json` | in `StoreApp/bin/Release/net8.0-windows10.0.19041.0/…/Assets/models/gemma-3-1b-it/` |
| License notices bundled | `GEMMA-NOTICE.txt` + `THIRD-PARTY-NOTICES.txt` | in `…/Assets/licenses/` (Content) |
| PodEval (envelope/plan logic) | **ALL PASS** | `dotnet run --project tests/PodEval -c Release` |
| CustodianGovernanceEval | **18 / 0** | `dotnet run --project tests/CustodianGovernanceEval -c Release` |
| PipelineGovernanceEval | **15 / 15** | `dotnet run --project tests/PipelineGovernanceEval -c Release` |
| ModelSmoke (real int4 model) | **PASS** | on-device plan (`provenance=local-model`, ≥1 admissible action, non-empty summary) + 2-turn no-echo chat |
| Backend live | health **200**, `verify-token` fail-closed (`paid:false`) | Cloud Run rev `pronto-pc-00076-hf5` (`…run.app/health`) |
| MSCERT2026 promo (reviewer code) | live, **14 redemptions left**, not expired | preflight `[1]` |
| Governance gate | **`enabled:false` (CLOSED)** | `MainWindow.xaml.cs` wires `new PipelineGovernance(enabled:false)` |

**The one non-blocking warning:** cloud fallback (Gemini) returns HTTP **429** (prepay credits depleted).
Not a cert blocker — the reviewer's repair plan is authored **on-device**, so the cloud path is never
required during review. This is the exact failure mode that caused the prior 10.1.2.10 rejection, now
durably removed by on-device authoring.

## 2. What the reviewer exercises (and what they do not)

- **On-device AI repair plan** — authored locally by the bundled int4 `gemma-3-1b-it` (no network
  dependency). This is the "AI-powered repair plan" claim, proven present by ModelSmoke.
- **Custodian actuator** — an on-screen vector figure that reflects the current step; a visual status
  indicator only, generates no text.
- **Conversational Technician** — present in the build but **gated closed** (`PipelineGovernance
  enabled:false`): the chat entry stays hidden, no inbound reaches the model, no peer-facing AI
  generation occurs. The certified build exposes **no ungoverned AI conversation**. (See
  `docs/PRONTOPC_AEGIS_INTEGRATION_CONTRACT.md` §14.)

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

These require production / Stripe / Partner Center / Windows-signing authority and are the reviewer
cert path. A Microsoft "PASS" is issued **after** submission and is not producible SI-side.

1. **Deploy** `pronto-server-cloudrun.mjs` to Cloud Run *(recommended, not blocking — the live rev
   already serves the reviewer flow; deploying makes the TOTEM-C-0046 await-payment reliability fix
   live)*, then positively verify `await-payment` with a real/comped checkout.
2. **Confirm Stripe LIVE**: `MSCERT2026` 100%-off + the `$29` Payment Link redirect to
   `success.html?session={CHECKOUT_SESSION_ID}`.
3. **Build the signed `.msixupload` v1.2.0.0** in Visual Studio — model bundled (confirmed above),
   identity `9P1K9QJXKND8`.
4. **Controlled end-to-end** on an admin test machine (repairs change system state — use no
   irreplaceable data); confirm the Technician entry stays **hidden**. Run **WACK**.
5. **Submit in Partner Center** with `MICROSOFT_STORE_CERT_NOTES.md` in "Notes for certification."
6. **On cert PASS** — promote `prontopc-repair-plan` provisional → verified (a Totem).

*Governance boundary for cert: `PipelineGovernance` ships `enabled:false`; opening the Technician (§8)
is a deliberately separate, post-cert decision.*

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
redirect fix is live). Governance gate stays `enabled:false`.

---

## Formed-model candidate rebuild — 2026-09-03 (TOTEM-X-0086)

This successor replaces the historical base-model package above as the current **unsigned
certification candidate**. It selects the Canon-formed CPU responder
`gemma-3-1b-it-formed-v0.2.1`; its formation manifest binds the frozen base, responder adapter,
locked Canon fingerprint, and the bounded Product Nature Layer. The Nature Layer supplies only
evidence-domain context alongside the validated SystemAudit. It never supplies facts or execution
authority. The peer-facing conversational governance gate remains `enabled:false`.

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

**Remaining external gates:** controlled installed-package E2E and WACK, Store signing/upload
workflow, and Partner Center submission.
