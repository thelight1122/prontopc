# ProntoPC — Manifest of Completion

Every build spec required to reach "Done." This is the fixed target an **Auditor**
compares the live code and system against. Each spec is a *claim about what must be
true*; the Auditor reports, per spec, whether reality matches — with evidence, no
judgement calls. "Ready" stops being an opinion and becomes a verdict.

Format per spec: `ID · claim · PASS criterion · where to verify`.
Status column is the author's current read, NOT the audit result — the Auditor overwrites it.

Governing principle (relational semiotics): **every worker the UI shows, and every
number it asserts, must correspond to real work the engine is actually doing. A sign
that cannot be backed by a true referent is a defect, not decoration.**

---

## TIER 0 — Certification-Ready  (already executable via certification_preflight.py)

- **C1 · Reviewer free access (policy 10.3.3)** — PASS if the Stripe payment link is
  active, `MSCERT2026` exists, is 100% off, has redemptions left, and is not expired.
  Verify: `certification_preflight.check_reviewer_access`. Status: PASS (last run).
- **C2 · Post-checkout path resolves (10.1.2.10)** — PASS if `prontopc.online`=200,
  `/success`=200, backend answers. Verify: `check_success_path`. Status: PASS.
- **C3 · AI repair plan genuinely present (10.1.2.10)** — PASS if `/api/repair-plan`
  returns a real plan that references supplied diagnostics AND is NOT the canned local
  fallback text. Verify: `check_ai_repair_plan`. Status: PASS *only while an LLM provider
  is billable* — see I2.
- **C4 · Countable claims match code** — PASS if repair-action catalogue ≥ 18, session
  window = 3 days, diagnostic categories ≥ 35. Verify: `check_countable_claims`.
- **C5 · Cert notes carry the access code + no-account statement** — Verify: `check_cert_notes`.

## TIER 1 — Product Integrity  (found gaps; not yet enforced)

- **I1 · One canonical scan+repair engine** — PASS if exactly one implementation is
  declared the shipping engine and the others are either removed or explicitly scoped
  (e.g. "web-only", "legacy") in a written ADR. FAIL today: three coexist (WPF
  `Repair.cs`, PowerShell web `/scan`+`/repair`, Python `ProntoAgent`) with no canon.
- **I2 · No silent non-AI fallback in the shipping client** — PASS if, when no LLM plan
  is available, the client either (a) blocks with an honest message, or (b) labels the
  result as an offline/basic plan — never presents `BuildLocalPlan` as the AI plan.
  FAIL today: `StoreApp/MainWindow.xaml.cs` silently substitutes `BuildLocalPlan`.
- **I3 · LLM provider resilience** — PASS if a primary (Azure OpenAI) and a fallback
  provider are both configured with live credentials, and provider health is checkable.
  Partial: fallback chain exists in `pronto-server-cloudrun.mjs`; both have failed live
  (401 / 429) — the 4th rejection. Long-term: embed a small local model (see roadmap).
- **I4 · Every repair action is reversible/safe as claimed** — PASS if each action in
  `Repair.cs` is non-destructive to user data and its result row is truthful. Status: PASS
  on read (system-tool + cache/temp deletion only); `remove_windows_old` is the one
  irreversible action and is gated on presence — confirm it is clearly disclosed.
- **I5 · Stripe $0 and paid both settle** — PASS if `verifyStripeSession` accepts `paid`,
  `no_payment_required`, and `complete`. Status: PASS (commit 91f1d13).

## TIER 2 — Relational-Semiotics UI  (the redesign contract)

- **U1 · No worker without a referent** — PASS if an Actuator is shown on stage only
  while its real operation is running or genuinely queued. FAIL if any scene is driven by
  a timer alone. (CustodianPrototype already enforces this in code — extend to the cast.)
- **U2 · HUDs assert only real telemetry** — PASS if every value shown (CPU %, MB, drive
  health, etc.) is sourced from actual `findings`/results, never a placeholder in shipped UI.
- **U3 · State-color is semantic** — PASS if green appears only on real completion and
  amber only for genuinely heavier/riskier operations.
- **U4 · Scene changes fire on real state transitions** — PASS if cube/scene advance is
  bound to backend state events, not `setInterval`. (Requires I3-adjacent progress signal
  for sub-steps, or an honest "working" hold instead of pantomime.)
- **U5 · Clean worker taxonomy** — PASS if each worker maps to one work class
  (Examiner=scan, Builder=repair, Verifier=confirm, Scout=recon); no mixed referents.
- **U6 · AEGIS palette applied consistently** — PASS if chrome / faceted-violet-gem /
  electric-blue / cyan-HUD tokens replace the legacy amber(#ffb95f)/blue/green across
  web (`success.html` et al.), WPF `StoreApp`, and `CustodianPrototype` (still on old palette).

---

## Auditor contract

The Auditor is a worker whose referent is *this Manifest vs. the live code/system*.
For each spec it returns: `id`, `verdict` (PASS | FAIL | PARTIAL | UNVERIFIABLE),
`evidence` (file:line or live response), and `what_would_make_it_pass`. It does not fix,
it does not embellish, it does not infer approval from silence. Any spec it cannot verify
is UNVERIFIABLE, not PASS. "Done" = zero FAIL on Tier 0 + Tier 1, and the agreed Tier 2
subset, with evidence attached to every PASS.
