# 1.2.21 repair-plan failure investigation — 2026-10-02

User reports the CertVM run now reaches the repair-plan retry screen after previously
working. Screenshot shows the application still open and reports a plan-build decline.
Treat this as a reliability shortfall; WACK does not establish scan-to-plan success.
No cause is established yet, and no app code or package has been changed in this turn.

Verified evidence:
- Totem ledger holds before diagnosis.
- Working archived 1.2.20 MSIX and current 1.2.21 MSIX have identical entry sets.
  Changes by ZIP size/CRC are AppxBlockMap, AppxManifest, ProntoPC.deps.json,
  ProntoPC.dll and ProntoPC.exe; model assets and native runtime entries unchanged.
- The diagnostic comparison tool compares normalized IL token names and generic
  type signatures for 147 methods across Diagnostics, SystemAuditor, ProductModel,
  OnnxGenAiInference, GemmaLocalPlanner, and Orchestrator (including generated
  async/iterator state-machine bodies). It reports zero normalized IL differences.
  This is code comparison evidence, not proof of the VM runtime outcome.
- Host C:/ProntoPC-Sandbox/source-sha256.txt records the previous 1.2.20 source hash
  9FD35FE4F5715DDABB3023B759BB2A24DE0805F264A9C089A56BCD54373E1377;
  ProntoPC-test.msix has October 1 staging time. VM installed version is unverified.
- Screenshot lacks the persistent disclosure footer added in 1.2.21, a clue that
  requires installed-version confirmation; display behavior remains another possibility.
- Codex handoff omitted explicit VM test-copy refresh instructions after 1.2.21 build.

Requested read-only evidence from the VM account that ran ProntoPC:

```powershell
Get-AppxPackage '*ProntoPC*' | Select-Object Name,Version,InstallLocation
$log = Join-Path $env:LOCALAPPDATA 'ProntoPC\logs\plan-authoring.log'
if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 60 } else { Write-Output "Log not found: $log" }
```

Next: use actual installed identity and decline reason to select diagnosis/fix.
Do not infer root cause from timing, absence of the footer, stale host staging,
or ModelSmoke/WACK. No VM restore, signing, trust update, install, submission,
commit, push, or speculative planner patch performed. Installation refresh, if
needed, must be explicit and tied to the exact 1.2.21 package.

## VM identity confirmed by user

PreFlight account reports installed ProntoPC 1.2.20.0. The failing screenshot/run
therefore does not exercise the new 1.2.21 reporting code. The ordinary log path
was absent; requested the possible MSIX LocalCache/Local redirected path.
The cause of the 1.2.20 plan-build failure remains undiagnosed.

Prepared owner-run scripts (syntax checked, not executed):
- Host Administrator PowerShell: tests/CertVM/prepare-12210-update.ps1 verifies
  the WACK-tested 1.2.21 source hash, signs a separate copy using the existing
  certificate, and copies it into the running VM through the existing enabled
  Guest Service Interface. It does not reset the VM or change certificate trust.
- VM PreFlight account: C:/CertTest/ProntoPC-1.2.21.0/install-12210-current-user.ps1
  checks the prepared file bindings and existing trust, updates that account
  without removing the previous package/data, and verifies installed package/DLL
  version. Stops if trust is absent, app still running, or prerequisites differ.

The agent performed no signing, installation, certificate trust mutation, or VM
write/reset. User execution of these commands is the next step. Original unsigned
Store/WACK artifacts are unchanged, so the prepared-copy operation will not
invalidate their WACK result. New-version full scan-to-plan acceptance remains unverified.

## Signing-certificate selection correction

Owner-run preparation stopped before signing because LocalMachine/My contains
three matching valid certificates. The script incorrectly required exactly one
publisher match. The previous working exported public test certificate is
CCC70E625DB54A1B2CFA523B91C90C21D7D94857. Its existing LocalMachine/My certificate
has matching publisher, private key, and current validity. Pin selection to that
exact thumbprint; preserve the guest existing-trust check. Script syntax and selector
were validated; no signing, trust change, or VM transfer was executed by Codex.

## Ten-second plan failure and verified source retry defect

User reports a plan-build failure in about ten seconds after the update workflow;
actual installed version and decline reason have been requested, not yet received.
Whether the app exits or remains on Retry has also been requested.

A concrete defect was independently reproduced in the existing retry path:
GemmaLocalPlanner.DetailReason adds semicolon-delimited token/error/preview metadata,
while Orchestrator matched the entire string against bare transient reason codes.
Diagnostic-suffixed empty completion and malformed JSON therefore skipped automatic
retry; cooldown selection also failed. Three regression checks failed before the
fix and pass after normalizing only the reason-code prefix for classification and
settle timing. Full diagnostic reasons remain intact. Deterministic declines still
are not retried. Full PodEval ALL PASS; Release build 0 warnings/0 errors.

PodEval's pre-existing project drift (LocalPlanner now references OnnxGenAiInference
but PodEval did not compile it) was reconciled by compiling the same real inference
source and referencing the already-used GenAI 0.15.2 package. No fake source stub.

This is a source correction only. The exact Store/WACK package remains 1.2.21.0
and does not include this fix. No new Store package, WACK, VM install, certificate
change or submission occurred. The fix does not establish the cause of the VM's
failure and cannot prove load/generation success there. Await actual installed
version and latest plan-authoring.log before selecting a complete release remedy.

## Installed 1.2.21 load decline, 2026-10-02
Owner guest log confirms version 1.2.21.0, zero input/output tokens, zero generation milliseconds, model_not_loaded. Underlying loader exception was swallowed. model_present=False is not reliable: logger checked obsolete Assets/Models/ProntoPlan folder. Source diagnostics now use ProductModel.DirectoryPath and preserve stage/type/message across warm-up and load cooldown. These source fixes are not in the unchanged WACK-tested package.

Read-only InstalledModelProbe uses installed GenAI managed/native libraries and model, with its own .NET 8 console host and explicit installed-library dependency resolution. Host package-layout probe passed model and tokenizer loading. This does not establish guest or installed-app acceptance. Owner must run as guest PreFlight with app closed and provide stdout/stderr. Release build passed with zero warnings/errors; PodEval ALL PASS (existing Windows-platform analyzer warnings). No new Store package, signing, trust, installation, submission, or remote VM write performed.

## Guest probe load access denial
Owner PreFlight probe at 2026-10-02T19:57:52Z confirms installed 1.2.21 and all five checked model files present, with expected sizes; onnxruntime.dll SHA256 matches host 18370C375F07357FA5874344A9D9AC17E6B6FE1EB18B1DD209D79483B4470257. LoadLibraryEx on that DLL returns Win32 error 5 (access denied), before GenAI/model construction. This is an external unpackaged probe and cannot alone prove the packaged app encounters the same access denial. Next evidence: fresh app plan failure followed by CodeIntegrity/Operational and AppLocker/EXE and DLL events to distinguish process/path/signing policy from app loader failure. Do not alter ACLs, take ownership, disable security or infer missing model/memory cause.

## No native policy events; probe v2
Owner CodeIntegrity and AppLocker queries returned no events in selection window. This supplies no affirmative policy-block evidence and does not exclude restrictions. Probe v2 defaults to direct GenAI Model construction without preloading onnxruntime.dll; uses process-local installed DLL search directory and installed GenAI DLL resolver. Explicit preload remains optional --preload for comparison. Host direct mode Model and Tokenizer PASS. External probe remains distinct from packaged app runtime. New zip InstalledModelProbe-12210-v2.zip; prior zip preserved. Guest direct-mode result pending.

## Guest v2 external managed-library denial
Owner reports direct-genai external probe FileLoadException access denied for installed Microsoft.ML.OnnxRuntimeGenAI.dll before Model constructor. Model files and both native library hashes still match host. Neither external probe reached the model, so neither establishes packaged-app model-loading cause. Stop external WindowsApps probing. Copy-VMFile attempted in guest PreFlight failed because it is a host Hyper-V command; existing v2 zip was present and executed, so no repeated copy required. Prior diagnostic harness restores CLEAN-BASELINE and mutates guest users; do not run it as a routine rollback. Baseline artifact is preserved and hash verified, actual installed app loader error remains pending. No new product package, install, trust, ACL or security change in this turn.
