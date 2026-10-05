# ProntoPC 1.2.23.0: final installed acceptance

Unsigned Store upload:
`H:/PRONTOPC_PRODUCTION/StoreApp/ProntoPC.msixupload`
SHA-256 `CB46C09815EA32CEE526BE9970219050FADFF4C0857E8BF0F77FDC747E2F17D1`.

Inner MSIX SHA-256
`F12EDA72707E8785D1BFD4223C7654CB0C449CC1A79F48D5C8BBC5B929796FE3`.
WACK PASS, APP_VERSION 1.2.23.0, report SHA-256
`A23FE37EE30F5598F4CD18D34DB3C3A999C774B969F6210546D233DCDF62CDBA`.

## Prepared test-copy update (not executed)

After explicit authorization for signing a test copy and updating the existing
Cert VM, run on the HOST as Administrator, with the existing ProntoPC-CertVM running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "H:\PRONTOPC_PRODUCTION\tests\CertVM\prepare-12230-test.ps1"
```

The script pins the unsigned source hash, uses only the existing matching test
certificate, signs a separate copy and stages it in the existing VM. It creates no
certificate and changes no trust. If VM access, guest service or certificate checks
fail, it stops. It does not restore a checkpoint or modify the Store upload.

Inside the VM, sign in as **CertUser** (or the already-established testing account;
use that same account for install and launch), close ProntoPC, and run ordinary PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CertTest\ProntoPC-1.2.23.0\install-12230-current-user.ps1
```

The installer checks the staged hashes, updates the current user's app and verifies
package version 1.2.23.0. Do not install from an old 1.2.21/1.2.22 script.

## One acceptance session

1. Cold-boot the VM, sign in to the same account, launch ProntoPC from Start. The
   read-only scan starts automatically. If model preparation is still incomplete,
   expect the truthful preparation message and muted robot video (or static fallback).
   The first attempt must reach findings without a customer Retry.
2. Click **Report this repair-plan summary**. Confirm the displayed summary is selected
   and editable. Redact it; verify the reviewed text in the email draft, copy and saved
   report. Opening a draft must not send automatically. Empty content must be refused.
3. Ask the Technician a question about a displayed finding. Click **Report this response**
   on its actual reply; confirm that exact reply is selected for review/redaction.
4. Click the persistent **Report inappropriate AI content** link; verify it works
   independently of the model and remains accessible at the tested window size.
5. Manually send one reviewed synthetic report with a unique marker to
   support@prontopc.online. Observe that marker in the actual support Inbox/Junk or
   routing folder. Record who monitors that mailbox and handles reported concerns.
   A draft, Sent Items, or lack of a bounce is not receipt evidence.
6. No repair execution is necessary for these reporting/preparation checks. Preserve
   observed results with account, timestamps, version and source/test-copy hashes.

Reviewer paste: `docs/CERT_NOTES_SHORT.txt` includes generative AI disclosure and the
exact reporting clicks. Confirm the Store description and Partner Center generative
AI declaration also reflect the feature before the owner presses Resubmit.

No installed PASS or mailbox receipt is claimed until actually observed. Microsoft
certification remains Microsoft's decision.
