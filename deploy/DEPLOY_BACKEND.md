# Backend deploy — Cloud Run env vars

The backend (`pronto-server-cloudrun.mjs`) deploys to Cloud Run service **`pronto-pc`**
(region `us-central1`) from source (buildpacks; `npm start` → the server, which reads
`PORT` from the env).

> **Drift rule:** run every command from the project root — each block below starts
> with `cd 'H:\PRONTOPC_PRODUCTION'`.

---

## The one thing the make-good actually needs

The AI-unavailable make-good reuses **`STRIPE_SECRET_KEY`** (mints the 100% coupon) and
**`RESEND_API_KEY`** (emails the customer + Support). Those are **already set** on the
live service (coupons/emails work today), and every make-good-specific var has a safe
default. So: **redeploying the new code is usually enough** — no new *required* vars.

**Prerequisite that is easy to miss:** `support@prontopc.online` must be a **verified
sender domain in Resend**, or every email (coupon, make-good, support) silently no-ops.

---

## Full env inventory

Secret = store in Secret Manager, never in `cloudrun.env.yaml` or git.

| Var | Purpose | Required? | Secret? | Default |
|-----|---------|-----------|---------|---------|
| `STRIPE_SECRET_KEY` | Mint coupons / verify payment | **Yes** | **Secret** | — |
| `RESEND_API_KEY` | Send all emails | **Yes** (for emails) | **Secret** | — |
| `FROM_EMAIL` | Email "from" | No | No | support@prontopc.online |
| `SUPPORT_EMAIL` | Make-good incident "to" | No | No | support@prontopc.online |
| `SITE_URL` | Links in emails | No | No | https://prontopc.online |
| `CLOUD_RUN_URL` | Self-reference | No | No | prod URL |
| `MAKEGOOD_COUPON_ID` | 100% coupon id (auto-created) | No | No | AIMAKEGOOD100 |
| `MAKEGOOD_VALID_DAYS` | 100% coupon validity | No | No | 180 |
| `COUPON_ID` | 50% next-repair coupon id | No | No | NEXTREPAIR50 |
| `COUPON_VALID_DAYS` | 50% coupon validity | No | No | 180 |
| `AZURE_OPENAI_ENDPOINT` | Cloud LLM fallback URL | No | No | (blank = off) |
| `AZURE_OPENAI_KEY` | Cloud LLM fallback key | No | **Secret** | — |
| `AZURE_OPENAI_DEPLOYMENT` | Azure deployment name | No | No | gpt-41-mini |
| `AZURE_OPENAI_API_VERSION` | Azure API version | No | No | 2024-10-21 |
| `GEMINI_API_KEY` | Legacy LLM fallback (if no Azure) | No | **Secret** | — |
| `LOYALTY_ADMIN_KEY` | Gate for `/admin/loyalty` | No | **Secret** | "" (locked) |
| `GUARDIAN_PRICE_MONTHLY` | Guardian sub price id | No | No | "" |
| `GUARDIAN_PRICE_WEEKLY` | Guardian sub price id | No | No | "" |
| `PORT` | Listen port | **Auto** | No | injected by Cloud Run — **never set** |

Non-secret values live in [`cloudrun.env.yaml`](cloudrun.env.yaml) (committed, no keys).

---

## Current live env (checked 2026-08-28)

The `pronto-pc` service holds **all 13 vars as PLAIN env vars** (no Secret Manager refs),
including the secrets `STRIPE_SECRET_KEY`, `RESEND_API_KEY`, `GEMINI_API_KEY`,
`AZURE_OPENAI_KEY`, `LOYALTY_ADMIN_KEY`, `STRIPE_WEBHOOK_SECRET`. `FROM_EMAIL` =
support@prontopc.online. **`STRIPE_SECRET_KEY` + `RESEND_API_KEY` are already present, so
the make-good works on a code-only redeploy.**

Issues found (not make-good blockers):
- **`GUARDIAN_PRICE_MONTHLY` is malformed** — its value is
  `price_1Ts7SDLLKFLplB2ZVANtEuBr GUARDIAN_PRICE_WEEKLY=price_1Ts7SDLLKFLplB2ZVEueHusZ`
  (a space instead of a comma mashed the weekly price into the monthly value; there is no
  real `GUARDIAN_PRICE_WEEKLY`). Guardian weekly pricing is broken until fixed. Fix (only
  if you use Guardian) — see option B below with:
  `GUARDIAN_PRICE_MONTHLY=price_1Ts7SDLLKFLplB2ZVANtEuBr,GUARDIAN_PRICE_WEEKLY=price_1Ts7SDLLKFLplB2ZVEueHusZ`
- Set-but-unused by the current server code: `STRIPE_WEBHOOK_SECRET`, `DOWNLOAD_URL`,
  `GUARDIAN_PRICE_ID`. Harmless cruft.

---

## Deploy — the SAFE path (env is all plain today)

⚠️ **Do NOT use `--env-vars-file` or `--set-secrets` on this service** — both *replace* the
whole env, and since the secrets live as plain vars, that would **wipe** them. (They only
become safe once every secret is migrated into Secret Manager — the optional hardening at
the bottom.)

**A. Code-only redeploy (recommended)** — new code, existing env preserved. The make-good
works immediately (Stripe + Resend already set; every make-good var defaults):

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud run deploy pronto-pc --source . --region us-central1
```

**B. If you also want the make-good vars set explicitly** — `--update-env-vars` *merges*
(keeps every existing var; adds/overrides only these). Non-destructive:

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud run deploy pronto-pc --source . --region us-central1 --update-env-vars SUPPORT_EMAIL=support@prontopc.online,MAKEGOOD_COUPON_ID=AIMAKEGOOD100,MAKEGOOD_VALID_DAYS=180,SITE_URL=https://prontopc.online
```

---

## Optional hardening — move secrets to Secret Manager (later, not required to ship)

Enter each value in the **Cloud Console** (Security → Secret Manager → *New version*) so it
never touches your shell history, or via CLI (`--data-file=-` reads stdin):

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud secrets create STRIPE_SECRET_KEY --replication-policy=automatic 2>$null; gcloud secrets versions add STRIPE_SECRET_KEY --data-file=-
```

Grant the runtime service account read access:

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud projects add-iam-policy-binding 108655745293 --member="serviceAccount:108655745293-compute@developer.gserviceaccount.com" --role="roles/secretmanager.secretAccessor"
```

Only *after* all secrets are in Secret Manager does the `--env-vars-file` +
`--set-secrets` form (listing every non-secret var from `cloudrun.env.yaml` and every
secret) become the correct, non-wiping deploy.

---

## 3. Verify after deploy

```bash
cd 'H:\PRONTOPC_PRODUCTION'; curl -s https://pronto-pc-108655745293.us-central1.run.app/health
```

Then in the production hot run, drive a real make-good (paid token → both AI authors
decline) and confirm the customer gets the 100% coupon email and Support gets the
incident email.
