# Retired Cloud Run Backend

This folder preserves the former ProntoPC Cloud Run backend source after the
one-time paid Microsoft Store app pivot.

The archived bundle contains:

- `pronto-server-cloudrun.mjs` — former HTTP service for Stripe checkout
  verification, paid-session tokens, native-plan/session recording, repair report
  rendering, refund helper paths, and AI-unavailable make-good coupons.
- `package.json` — former Cloud Run Node package manifest.
- `.gcloudignore` — former Cloud Run source-upload filter.
- `deploy/` — former Cloud Run environment and deployment notes.
- `ProntoAgent/` — former Python desktop/CLI agent source that requested repair
  plans from the Cloud Run API.

This is archive evidence only. The current StoreApp path no longer depends on a
runtime payment server, website checkout token, Cloud Run session lifecycle,
Firestore paid-session state, or server-rendered repair report. Do not use this
folder as deployment authority without a fresh owner gate.
