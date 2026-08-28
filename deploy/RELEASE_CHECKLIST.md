# ProntoPC — release checklist (repair-plan: provisional → verified)

Pre-authorize the **[A] batch** once and Claude runs it straight through, pausing only on a
failure or a genuine surprise. The **[B] items** physically require Tracey (a UAC click, a
browser dashboard, Visual Studio / Partner Center, or an external tool) — Claude can't do them.

Home base for every command: `H:\PRONTOPC_PRODUCTION`.

---

## [A] Claude runs these on your one-time approval

- [ ] **A1 — Backend deploy (code-only, env preserved).** `gcloud run deploy pronto-pc
  --source . --region us-central1`. Ships the make-good code. Env untouched (Stripe + Resend
  already live). Reversible: Cloud Run keeps the prior revision for instant rollback.
- [ ] **A2 — Post-deploy verify.** Hit `/health`; confirm `POST /api/report-ai-unavailable`
  responds (400 on missing token = wired). Report result.
- [ ] **A3 — (optional) Fix the malformed Guardian price.** Only if you use Guardian:
  `--update-env-vars GUARDIAN_PRICE_MONTHLY=price_1Ts7SDLLKFLplB2ZVANtEuBr,GUARDIAN_PRICE_WEEKLY=price_1Ts7SDLLKFLplB2ZVEueHusZ`.
  Say yes/no on this one.
- [ ] **A4 — Final trust flip (AFTER your B3 + B4 succeed).** Promote `prontopc-repair-plan`
  provisional → verified in `pod.json` + `orb.json`, append the Totem, update CURRENT-STATE,
  commit. Claude waits for your "hot run + cert passed" before doing this.

## [B] Only you can do these

- [ ] **B1 — Verify the Resend sender domain** for `support@prontopc.online` (Resend
  dashboard). Without it, coupon + make-good emails silently no-op. Do before B3.
- [ ] **B2 — Rotate the `VERCEL_OIDC_TOKEN`** (Vercel dashboard) — tidy-up from the System32
  detour. Low urgency.
- [ ] **B3 — Production hot run.** Real paid token → run the WPF app (UAC) → success path,
  then force a real make-good; confirm the customer 100% coupon email + the Support incident
  email arrive. (Claude can read the deployed server logs afterward to confirm the
  `[ai-unavailable]` line.)
- [ ] **B4 — Codex code audit.**
- [ ] **B5 — Store cert.** VS → Create App Packages → `.msixupload` (identity already set) →
  Partner Center → resubmit.

---

## Order / dependencies
`A1 → A2` now. `B1` before `B3`. `B3` needs `A1` done. `A4` after `B3` + `B5` pass.
`B4` can run anytime after `A1`. `A3`, `B2` are independent.
