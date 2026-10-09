# Separate-account UAC diagnostic — October 8, 2026

Status: isolated fixture prepared; no visible UAC outcome observed.

Recovery checkpoint `1cefab0fb225ed06715f1a827fccbf71ec25fbe6` is pushed and its
remote SHA verified. The original working tree and excluded artifacts are preserved
on a separate physical SSD; see `RECOVERY_CHECKPOINT_20261008.md`.

## Evidence established before the test

- Original VM: `ProntoPC-CertVM`. Read-only guest queries identify local standard
  account `CertUser` and administrator `PreFlight`, with UAC enabled and credential
  prompting for standard users. The currently present app is 1.2.26.0; its DLL and
  executable hashes match the previously recorded 1.2.26 payload. Per-account
  registration was not resolved by the initial collector. Original guest OS:
  26H2 build 26300.9457, different from the reviewer build reported in C-0201.
- `quser` failed with `No User exists for *`. The corrected collector retained
  the other successful queries; it did not convert missing session data into a
  claim about the interactive desktop. Credentials stayed in memory and the
  read-only session was closed after inspection.
- Preserved unsigned 1.2.25 MSIX SHA-256:
  `149F2EC38B10FB28AF026586D0290802FDF42B94E2B80797B1E59CB2C53D7535`.
- Preserved 1.2.25 MSIXUPLOAD SHA-256:
  `0B437B154E39401E872ED7BD019C77573536D2EA1388228EE46F06B67FCBA1E8`.
- Existing signed 1.2.25 VM test-copy SHA-256:
  `22C455096294D59F88CF5DE3198748DE3AD5652E7E5FC1791378BEA92C9BAB77`.
- Fresh signed/unsigned comparison: all 506 common entries match except signing
  metadata in `[Content_Types].xml`; only signature/catalog entries are added.
  No application payload entries were removed. No new signing was performed.

Exact query and payload evidence:
`UAC_125_ORIGINAL_VM_INVENTORY_20261008.json` and
`UAC_125_SIGNED_PAYLOAD_REVERIFICATION_20261008.json`.

Source qualification: the repair request is passed as an absolute path and the
child reads that supplied path. A change of account does not itself demonstrate
that the child looks in its own LocalAppData instead; actual access failure or
package/loader failure still needs runtime evidence.

## Isolated fixture

Diagnostic VM: `ProntoPC-UAC-125-20261008`, copied from the original VM's existing
`Cert_User_added` checkpoint. The export and independent imported runtime are under
`G:\AEGIS_RECOVERY_BACKUPS\ProntoPC-20261009T005215Z\cross-account-125-vm`.
The preparation script disconnects networking on the new diagnostic copy, starts
it, and copies the already signed 1.2.25 package and the guest test script into
`C:\CertTest\CrossAccount-125`.

Preparation completed at `2026-10-09T01:18:29Z`. Diagnostic VM ID:
`ce492ab9-1f23-4d22-bc0c-5e186cfde352`. Its Virtual Machine Connection window
was opened and identified by title. Package transfer completed; the guest helper
will independently verify the delivered package hash before installation.
Host preparation evidence: `UAC_125_ISOLATED_VM_PREPARATION_20261008.json`.

The original VM is not restored or downgraded. No certificate trust is imported.
The diagnostic snapshot's OS/account/dependency/trust state must be checked by
the guest script; the original VM's inventory is not substituted for it.

## Owner action in the diagnostic VM

1. Open `ProntoPC-UAC-125-20261008` in Hyper-V Virtual Machine Connection.
   Sign in as **CertUser**. Open ordinary Windows PowerShell inside that VM.
2. Run:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "C:\CertTest\CrossAccount-125\run-cross-account-125.ps1"
   ```

   Expected: `EXACT 1.2.25 PREPARED` followed by a ProntoPC window. The script
   verifies the standard account/token, UAC, existing certificate trust, registered
   VCLibs dependency, signed-package hash and installed DLL. It installs or updates
   this account to the preserved 1.2.25 only when no newer version is registered.
   If a guard fails, retain that error and stop; do not change trust, remove
   packages, or bypass the check.
3. Run **Scan → Findings → Perform Repairs**. At UAC, use the existing guest
   administrator **PreFlight**. Record whether an error happens at initial launch
   or only at the repair/elevation step, and whether the repairs/report complete.
4. In the same ordinary PowerShell window, capture the result. If the visible
   error is `0xc0000142` after the UAC step, run:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "C:\CertTest\CrossAccount-125\run-cross-account-125.ps1" -Mode Capture -AdminAccountUsed PreFlight -ObservedOutcome Native0142
   ```

   Use `RepairsCompleted`, `UacCancelled`, `Other`, or `Unobserved` instead only
   when that is the actual result. The outcome parameter is explicitly recorded
   as the operator's declaration, separately from Windows events and copied logs.

Evidence is written to a new per-run directory under
`$env:LOCALAPPDATA\ProntoPC-CrossAccount-Tests`, with separate capture directories
for repeat observations. Missing events do not establish success. The script does
not automatically start repairs or answer UAC.

## Current limits

The guest helper passed PowerShell parsing and its host/wrong-account guard was
executed successfully: it refused to run outside CertUser before any preparation
or installation. Its guest preparation, UI/UAC run and capture remain unobserved
until the operator performs the steps above. This diagnostic is not a new product
build, elevation fix, preflight PASS, or submission authorization.
