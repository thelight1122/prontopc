# ProntoPC 1.2.26.0 VM acceptance

Run inside the existing **ProntoPC-CertVM**, signed in as **PreFlight**. Close the old ProntoPC window. In ordinary PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CertTest\ProntoPC-1.2.26.0\install-12260-current-user.ps1
```

Expected: `UPDATE VERIFIED: ProntoPC 1.2.26.0 for the current account.` Stop on any error; do not import certificates or change trust.

1. Open ProntoPC from Start. Let the first scan reach findings.
2. Confirm the plan does not list individual temporary-file cleanup alongside the broader disk-cleanup action when the latter is present. DNS and power-setting actions may still appear; they are not space recovery.
3. Perform the selected repairs, approving the repair-step Windows prompt. Open Report.
4. The summary must give the actual completed-action count and recorded space recovered. Its count and recovered total must agree with the completed rows. A fresh run may recover a different amount or 0 MB; do not expect the previous run's14 MB. Drive usage is a scan measurement. The summary must not claim numerical improvements to speed, DNS, or power use.
5. Open the persistent Report inappropriate AI content dialog and confirm Copy report and Save report still work. Support delivery and mailbox monitoring were confirmed for1.2.25; no repeat email is needed just to prove that same mailbox.

Return the new HTML report and whether the first scan reached findings without Retry and Copy/Save worked. These are new-artifact observations; earlier1.2.25 acceptance remains separate.

Unsigned Store upload SHA-256: `B25026F529249CC32734A85E34C0F2446EE2EB8126D9D0B1D63F6DA35025A4AD`.
Signed VM copy SHA-256: `D5922491AA5A0B40024FD20B605B934522EE701DAD010103BB0222777A69F566`.

No Partner Center submission has been performed.
