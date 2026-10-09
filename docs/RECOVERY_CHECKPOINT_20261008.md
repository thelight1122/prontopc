# ProntoPC recovery checkpoint — October 8, 2026 (America/Phoenix)

Status: work preserved; certification incomplete. This checkpoint preserves existing
work after the owner reported a power-supply failure and three unavailable SATA
drives. It does not certify a release or resolve the reported elevation failure.

## Repository scope

Base commit: `7d15506d885c21997d0532207af81d9f0e0be761` on
`prontopc-1.2.20-rollup`. The live remote branch was independently queried and
matched that SHA before this checkpoint.

The checkpoint contains the 19 previously modified tracked files, 32 previously
untracked files, and this recovery record. The original changes include the
1.2.26.0 report-summary corrections, overlap cleanup handling, packaging correction,
tests, CertVM scripts, and historical acceptance/certification evidence. Six existing
uncommitted Codex-lane Totems (X-0195 through X-0200) are preserved unchanged;
the recovery successor is appended in the same lane.

The unreferenced `assets/PRONTO_ROBOT_WAKEUP.mp4` and root analytics npm manifests
are preserved as existing source material. No app video integration, npm install,
website deployment, new package build, signing, trust change, or submission occurred.

Historical `CERT_READY` records remain historical. In particular, they do not
cover the separate-account UAC case in Verus's `TOTEM-C-0201`.

## Independent recovery copy

Location: `G:\AEGIS_RECOVERY_BACKUPS\ProntoPC-20261009T005215Z`.

The checkout is on H: (physical disk 0); this copy is on G: (physical disk 1).
Both disks are currently local NVMe devices. This is a separate physical-disk copy,
not a claim of offsite or disconnected storage.

The completed initial copy contains 2,906 files totaling 34,411,935,127 bytes:
tracked and nonignored untracked workspace files, selected ignored release packages,
model weights, WACK reports, signed test packages, available owner evidence ZIPs,
and a verified all-refs Git bundle made before the checkpoint commit. Reproducible
build/cache directories and credentials/private keys were not selected. This is
not a backup or recovery of the unavailable drives, whole host, or entire VM.

Every copied file's SHA-256 matched its source, with the source checked again after
copying. `MANIFEST.json` SHA-256:
`98CE8B7CE99B640F5221AFF446ACD47DBC26F173E04190312045981ACF25150C`.
Post-checkpoint commit/remote evidence and an updated Git bundle are recorded
separately in the recovery directory after the push.

## Verification performed now

- `git diff --check` on the original tracked edits: passed. The later staged check
  also inspects newly preserved files and flags trailing whitespace in four original
  console/transcript records and final blank lines in three original scripts. Those
  bytes are retained to preserve their historical hash bindings.
- `dotnet run --project tests/PodEval/PodEval.csproj --no-restore`: exit 0, ALL PASS,
  including outcome-based report summaries and overlapping cleanup regressions.
  Existing Windows-platform analyzer warnings were emitted.
- Changed text inspection: 49 text files; 20 JSON files parsed successfully;
  all JSONL records parsed; no matches for the checked private-key, GitHub-token,
  OpenAI-key, or AWS-access-key patterns. This is a bounded scan, not an exhaustive
  security certification.
- Codex shard: committed prefix unchanged; six original new lines preserved.
- The three existing 1.2.26 CertVM scripts passed PowerShell parsing.
- Totem verifier: drift remains for four missing historical Exchange message paths
  under the former `I:/AEGIS-CENTRAL-CODEX/.../exchange/from-claude/` location.
  Prior records were not rewritten or falsely retired to hide that loss.

## Next diagnostic step

Verus's C-0201 reports rejection of 1.2.25.0 with `0xc0000142` and proposes an
unconfirmed separate-account UAC explanation. Source inspection confirms that
`RequestElevatedRepair` relaunches the same executable with `Verb = "runas"`,
passes an absolute request path under the initiating user's LocalAppData, and
exits the parent after `Process.Start` returns. This establishes the code path,
not the reviewer's account configuration or the cause of DLL initialization failure.

An elevated read-only host inspection confirms that the existing
`ProntoPC-CertVM` is running normally, with Guest Service Interface enabled and
its existing checkpoints intact. No checkpoint was restored. A preserved signed
1.2.25 test copy exists at
`C:/ProntoPC-Sandbox/update-12250-20261005-055400-067/ProntoPC-test.msix`;
use its exact historical hash and payload bindings when preparing the reproduction.

Inspect guest account membership, package registration, OS build and UAC state;
then test Perform Repairs as a standard user with a different existing administrator
at the Windows prompt. Record observed results against the exact package. Do not
infer causality from source inspection, substitute the existing 1.2.26 acceptance,
restore the VM, downgrade an existing user, or mutate trust as a shortcut.

Partner Center upload remains owner-controlled.
