# ProntoPC Cert VM (Hyper-V)

A persistent, checkpointed stand-in for a Microsoft cert device. It covers the gap the
Windows Sandbox leaves open (cert 10.1.2.10 on 2026-09-30, `0xc0000142`): the Sandbox runs
as admin with UAC off, so Perform Repairs' elevated relaunch never really happens there.

| Property | Setting |
|---|---|
| Account under test | **CertUser**, standard user, UAC on |
| Admin | **CertAdmin**, used only to answer the UAC prompt |
| VC++ redist / dev tools | None |
| GPU | None (software rendering; the model is CPU-only) |
| Reset | `CLEAN-BASELINE` checkpoint |

## One-time setup (host, elevated PowerShell, in this folder)
1. `.\new-certvm.ps1`
2. In the VM window: boot from DVD, install **Windows 11 Pro**, create **CertAdmin**. Skip optional extras.
3. In the VM as CertAdmin: `net user CertUser <password> /add` (standard by default).
4. Optional, to match the cert build: Windows Update to 25H2 (26200). The ISO is 24H2 (26100).
5. Host: `.\baseline-certvm.ps1`

## Each candidate
Shortcut: `.\run-certvm-diagnosis.ps1` does steps 1-3 (and one-time CertUser + baseline) in one elevated run.
1. Host: in `tests\CleanSandbox`, `.\prepare-host.ps1 -Msix 'H:\PRONTOPC_PRODUCTION\StoreApp\ProntoPC.msix' -Label FIX`
2. Host: `.\stage-package.ps1` (reverts, boots, copies to `C:\CertTest`)
3. VM as CertAdmin, elevated: `powershell -ExecutionPolicy Bypass -File C:\CertTest\install-in-vm.ps1`
4. Sign out, sign in as **CertUser**.

## The run (as CertUser)
Start → ProntoPC → Scan → Findings → **Perform Repairs** → UAC asks for CertAdmin's password →
repairs complete → report opens. Screenshot any error dialog.
`0xc0000142` at Perform Repairs confirms the elevated-relaunch diagnosis.
