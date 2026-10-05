# ProntoPC 1.2.24.0 Cert VM acceptance

Use the existing Cert VM, signed in as PreFlight. Close ProntoPC before updating.

In ordinary PowerShell INSIDE the VM, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CertTest\ProntoPC-1.2.24.0\install-12240-current-user.ps1
```

Expected: installed package and DLL version 1.2.24.0. Stop on any error and preserve the output.

Launch ProntoPC. Observe automatic scan, genuine AI repair plan, a Technician answer, and repairs completing. No warm-up video or screen should appear. Open Report this repair-plan summary and Report this response; confirm the selected text is editable, support@prontopc.online is visible, and Copy report / Save report are the first actions. Paste copied text into a text editor and inspect a saved report. The optional email-app button is explicitly labeled as requiring an installed app.

Send one reviewed sample report to support@prontopc.online using your normal email route, then verify receipt and confirm who monitors the address. Copy/Save alone proves report creation, not delivery or monitoring. Record results against the exact upload hash in docs/CERT_RELEASE_12240.json. These final observations remain pending until actually performed.

Use docs/CERT_NOTES_SHORT.txt for the Partner Center reviewer notes. Upload remains with Tracey.
