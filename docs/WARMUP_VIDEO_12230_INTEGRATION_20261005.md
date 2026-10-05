# ProntoPC 1.2.23.0 source candidate — awaited model preparation

Owner-edited source video: `assets/PRONTO_ROBOT_WAKEUP.mp4`.
SHA-256: `720C73BAFC6867FA03416A12C918333AB2E5105A20440EE32E01991537C11DC1`.
Windows media metadata: 9.408 seconds, 1920×1080, 30 fps, two audio channels.
Playback in ProntoPC is explicitly muted. The original file was not transcoded or edited.
The project links it as `Assets/warmup/PRONTO_ROBOT_WAKEUP.mp4`; the Release output
copy has the same hash. Existing wake/loop clips remain preserved but are not used by
this integration; the owner-edited video is the selected visual.

Model preparation starts during the read-only scan as one retained Task. Before plan
generation, the UI awaits that Task. If it is still running, the Custodian explains
on-device preparation and the edited video plays. The video repeats only while
preparation remains incomplete; successful loading ends the visual immediately,
without waiting for a fixed animation duration. If preparation already completed
during the scan, the app does not insert an artificial pause. Media failure falls
back to the auditor image and the same truthful caption, without blocking loading.

The loader now propagates construction failures after disposing partial state.
Preparation owns up to three load attempts with three-second pauses and visible
retry captions. It logs elapsed time, attempt, version and exception type/HResult/
message to local plan-authoring.log. Plan generation cannot begin on failed
preparation. Exhausted preparation shows a truthful error rather than requiring the
customer to press the plan Retry button. This does not establish that all plan
generation failures are solved, or prove reliability on a clean review machine.

## Observed validation

- Release source build: PASS, zero warnings/errors (including version 1.2.23.0).
- `dotnet run --project tests/ModelSmoke/ModelSmoke.csproj -c Release`: PASS.
  Real model preparation: 4,759 ms, first attempt. Genuine local-model plans on
  representative and clean reviewer-style audits; grounded two-turn chat PASS.
- `dotnet run --project tests/ModelWarmupEval/ModelWarmupEval.csproj -c Release`:
  four PASS scenarios: missing configuration, recovery after one failed load,
  exactly three attempts on persistent memory failure, cancellation before retry.
  Synthetic failures are test inputs, not observed native runtime defects.
- Video source/output SHA-256 equality: PASS.
- `git diff --check`: PASS.

## Remaining release gates

No new MSIX/MSIXUPLOAD has been produced in this turn. The preserved active upload
is still baseline 1.2.20.0 and does not contain these changes. Current source is a
new 1.2.23.0 candidate, not CERT_READY. Reporting controls remain archived separately
and must be integrated into the intended release. Then package inspection, fresh
artifact-bound WACK/preflight and cold-boot Cert VM first-use acceptance are needed.
Installed video decode/playback (including a no-GPU guest, scaling and media failure),
actual warm-up captions under slow load, and mailbox receipt/monitoring remain
unverified. No signing, installation, trust change, repairs, Partner Center action,
commit or push occurred in this turn.
