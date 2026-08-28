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

## 1. Secrets → Secret Manager (once)

Easiest: enter the value in the **Cloud Console** (Security → Secret Manager → the secret
→ *New version*) so the key never touches your shell history. CLI alternative (paste the
value at the prompt; `--data-file=-` reads stdin):

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud secrets create STRIPE_SECRET_KEY --replication-policy=automatic 2>$null; gcloud secrets versions add STRIPE_SECRET_KEY --data-file=-
```

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud secrets create RESEND_API_KEY --replication-policy=automatic 2>$null; gcloud secrets versions add RESEND_API_KEY --data-file=-
```

Grant the Cloud Run runtime service account read access (replace `PROJECT_NUMBER`):

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud projects add-iam-policy-binding 108655745293 --member="serviceAccount:108655745293-compute@developer.gserviceaccount.com" --role="roles/secretmanager.secretAccessor"
```

If Stripe/Resend are already set on the live service (as plain env vars from an earlier
deploy), you can skip Secret Manager and just keep them — but moving them to secrets is
the safer end state.

---

## 2. Deploy

```bash
cd 'H:\PRONTOPC_PRODUCTION'; gcloud run deploy pronto-pc --source . --region us-central1 --allow-unauthenticated --env-vars-file deploy/cloudrun.env.yaml --set-secrets "STRIPE_SECRET_KEY=STRIPE_SECRET_KEY:latest,RESEND_API_KEY=RESEND_API_KEY:latest"
```

Add optional secrets to the same `--set-secrets` list if used, comma-separated, e.g.
`AZURE_OPENAI_KEY=AZURE_OPENAI_KEY:latest,LOYALTY_ADMIN_KEY=LOYALTY_ADMIN_KEY:latest`.

`--env-vars-file` and `--set-secrets` **replace** the service's env each deploy, so list
every var you want on the service (the yaml covers the non-secret ones).

---

## 3. Verify after deploy

```bash
cd 'H:\PRONTOPC_PRODUCTION'; curl -s https://pronto-pc-108655745293.us-central1.run.app/health
```

Then in the production hot run, drive a real make-good (paid token → both AI authors
decline) and confirm the customer gets the 100% coupon email and Support gets the
incident email.
