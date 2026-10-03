# ProntoPC 1.2.21.0 observation checklist — October 3, 2026

This is the archived reporting candidate, not the active baseline. Latest Codex
Totems X-0176/X-0177 restore active 1.2.20.0; X-0178 prepares a separate 1.2.22.0
loader diagnostic. Do not install, sign, alter trust, or restore a VM as part of
these read-only reporting checks. No Partner Center submission is authorized.

## Exact artifact and observations

- Upload: `H:\PRONTOPC_PRODUCTION\release-candidates\disclaimer-enhancements-20261002\ProntoPC.msixupload`
- Upload SHA-256 (freshly verified): `8D2FF6B1867FE22BE1612A239B906F80E931EBD9BBB6ED48993DC1B36CFD82C3`
- Inner unsigned MSIX expected SHA-256: `228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655`.
- Fresh host current-account package query: 1.2.10.0, not 1.2.21.0.
- Prior guest evidence: 1.2.21.0 model_not_loaded, zero generation tokens/time.
  External probe access denials do not establish the packaged app loader cause.
- Installed reporting flow: unverified. Support receipt: unverified. Monitoring: unverified.
- Existing reporting verification JSON retains false flags. No email sent by Codex.

## Current-account identity and payload binding

Inside the Cert VM, sign in as **PreFlight**, the account that will launch the app.
Use ordinary PowerShell. Do not run these commands in the host Administrator shell.

```powershell
whoami
$candidate = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
$candidate | Select-Object Name,Version,PackageFullName,PackageFamilyName,InstallLocation
if (-not $candidate -or $candidate.Version -ne '1.2.21.0') { throw 'Stop: this account does not have the reporting candidate installed.' }
[Reflection.AssemblyName]::GetAssemblyName((Join-Path $candidate.InstallLocation 'ProntoPC.dll')).Version.ToString()
$bindingDir = 'C:\CertTest\ProntoPC-1.2.21.0'
Get-Content -LiteralPath (Join-Path $bindingDir 'source-sha256.txt')
Get-Content -LiteralPath (Join-Path $bindingDir 'signed-sha256.txt')
Get-FileHash -LiteralPath (Join-Path $bindingDir 'ProntoPC-test.msix') -Algorithm SHA256
```

The DLL version must also be 1.2.21.0. Source binding must equal the inner unsigned
MSIX hash above. Signed-copy file hash must equal signed-sha256.txt. These sidecars
alone do not prove installed payload equivalence: preserve the preparation/install
transcript and compare installed ProntoPC.dll SHA-256 with ProntoPC.dll extracted
from the exact archived unsigned candidate. If provenance or permissions prevent
that comparison, record the gap; do not change ACLs or claim exact-package PASS.
The signed test-copy hash differs from the unsigned upload hash by design; record
both and their verified payload relationship. If any identity/binding check fails,
stop this candidate test and report the result. Do not reinstall using stale scripts.

## Installed reporting clicks

1. Launch ProntoPC from Start as PreFlight. Record timestamp and visible version.
   Click **Report inappropriate AI content** in the persistent footer before a scan.
2. Enter synthetic content and a synthetic concern; include a unique marker such
   as `ProntoPC-12210-report-test-YYYYMMDD-HHMM`. Review and redact the text.
3. Open the email draft. Verify recipient `support@prontopc.online`, subject
   `ProntoPC AI content report`, version 1.2.21.0, marker, concern and reviewed
   content. Confirm no unexpected private logs/history. Cancel sending.
4. Reopen the dialog. Use **Copy report**; paste into a local text editor and
   verify that only the reviewed text is copied. Use **Save report** and inspect
   the text file. Exercise these alternatives on an account with no mail app
   configured if available; otherwise mark that scenario untested.
5. Check that empty content cannot create a report and redactions persist in
   draft/copied/saved output. Record any UI obstruction at the tested scaling.
6. Run scan → findings → AI repair plan. If it declines, record the displayed
   result and relevant log timestamp; leave plan/Technician reporting untested.
   Do not label runtime acceptance PASS based on WACK or ModelSmoke.
7. If a plan is actually displayed, click **Report this repair-plan summary**;
   verify the exact displayed summary is selected and editable. Also exercise
   the global report action for that output.
8. Ask the Technician a findings-grounded question. Click **Report this response**
   on an actual generated reply; verify exact selected text is editable before
   draft/copy/save. Do not substitute synthetic text for this selected-reply test.
9. Click **About AI & AEGIS** and record the actual opened URL and page result:
   `https://prontopc.online/ai-aegis`. Check footer availability through observed
   phases; any phase not reached remains untested.

## Separate owner mailbox receipt and monitoring check

The draft test above does not establish delivery. The owner may send one reviewed
synthetic report manually with the unique marker. Codex has not sent a message.

1. Before sending, preserve recipient, subject, marker, version and send time.
2. In the actual support mailbox, search Inbox, Junk/Spam, quarantine and any
   routing folders for that marker. Record received time, recipient and subject,
   and a private message reference or redacted screenshot. Sent Items, mailto
   opening, DNS records or absence of a bounce alone do not prove receipt.
3. If absent, record last checked time, folders checked and any actual bounce or
   routing error. Leave receipt unverified until the received message is seen.
4. Record the responsible monitor, real access to the mailbox, agreed check
   cadence and actual review timestamp. Document concern triage, disposition
   and response process using docs/AI_CONTENT_REPORTING_PROCEDURE.md. A proposed
   cadence alone does not prove monitoring is operating.
5. Keep personal report content out of Totems. Evidence should contain only
   synthetic/redacted markers and minimal references to private support records.

## Record each result separately

Use: UTC timestamp; observer; account/VM; upload SHA-256; unsigned MSIX SHA-256;
signed-copy SHA-256 and provenance; installed identity/DLL binding; exact action;
observed result (PASS/shortfall/untested); minimal evidence path/reference.
Do not set installed_reporting_flow_pass until all required installed scenarios
are observed. Do not set support_receipt_and_monitoring_confirmed until receipt
and actual monitoring are both evidenced. Retain earlier observations and append
new results. Report a declined AI plan even if the reporting controls work.

## Fresh preflight interpretation

The separate snapshot `docs/CERTIFICATION_PREFLIGHT_12210_RECHECK_20261003.json`
uses the CURRENT restored-baseline preflight against the archived upload. It mixes
current source checks with archived package inspection and therefore is a
compatibility audit, not candidate acceptance. It does not contain the archived
v3.5 reporting gate. Source/package version mismatch is expected and blocking.
Historical 22/23 and WACK PASS must not be promoted into current readiness.
Submission remains blocked by candidate/source mismatch, unverified reporting and
receipt/monitoring, and unresolved installed AI plan acceptance.

### Proceed follow-up — observed host checks, 2026-10-03

Host cannot query Hyper-V: Get-VM returned permission denied. Outlook delegated
folder discovery for exact support@prontopc.online returned 404 ErrorInvalidUser;
this proves connector access failed, not that the mailbox or delivery is broken.
Preserved signed test MSIX hash verified against its sidecar:
01BD00F53C4E4ABFCBEB0FB4835A19DC381ABBA24C8D560A6EBBC8DC65097464.
Unsigned/signed ZIP comparison: 505 common entries byte-identical; only changed
common entry [Content_Types].xml; signed-only entries CodeIntegrity.cat and
AppxSignature.p7x. Candidate DLL SHA-256 in both:
9DD5B9E8E9646BDCDCF145A229E098F9B95CA1642628DE8FF3B92E0EA240AF68.
This is preserved-copy equivalence evidence, not fresh installed guest acceptance.
Read-only guest capture script: tests/CertVM/capture-12210-reporting-identity.ps1
(PowerShell syntax PASS; not executed in the guest). Host observations saved in
docs/CERT_GATE_12210_HOST_OBSERVATIONS_20261003.json. All reporting/receipt/
monitoring results remain unverified. No WACK rerun or Partner Center action.
