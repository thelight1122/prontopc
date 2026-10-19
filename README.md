# ProntoPC

**Your PC. Restored. Pronto.**

ProntoPC is a Windows PC-repair app sold on the Microsoft Store. It scans your PC, explains what it found in plain English, and, if you approve, carries out the recommended repairs. The AI that writes the repair plan and answers your questions is bundled inside the app and runs entirely on your computer.

- **One-time purchase of $9.99** through the Microsoft Store. No subscription, account, sign-in or per-run fees.
- **Works offline.** Scan, plan, chat, repair and report use no network connection.
- **You stay in control.** The scan only reads. Nothing changes until you approve it, and Windows asks for administrator approval only at the repair step.

## How it works

1. **Launch ProntoPC.** Your Custodian Technician, an on-device AI guide, walks you through each step.
2. **The scan runs.** A read-only diagnostic checks six areas in a few minutes:
   - Storage and disk health
   - Drivers and hardware
   - Security
   - Network
   - Performance
   - System integrity
3. **Review the findings.** The Technician explains each finding and answers your questions. A full HTML report is saved to your Desktop.
4. **Your choice.** Uncheck anything you don't want, then select **Perform Repairs**, or keep the report and decide later. You can run ProntoPC as often as you like.

## On-device AI

The repair plan summary and the Technician's replies are written by **Google Gemma 3 1B**, bundled in the app as an int4 ONNX model and run locally with ONNX Runtime GenAI. No diagnostic data leaves the PC to build the plan.

The AI explains the results of a deterministic scan. It does not decide what was found. If the on-device model can't produce a plan, the app says so plainly and offers a support path. It never presents a rule-based plan as an AI plan.

The model's license notices (`GEMMA-NOTICE.txt` and `THIRD-PARTY-NOTICES.txt`) ship with the app and can be opened from the in-app **AI model & licenses** link.

## Built on AEGIS

ProntoPC is built with the AEGIS Pod architecture and governance principles from the [AEGIS Initiative](https://doi.org/10.5281/zenodo.22670157):

- **Pod, Orbs and Portals.** A thin orchestration core, with each capability (repair engine, diagnostics, repair plan, Custodian stage) attached through a declared contract. See [`pod/README.md`](pod/README.md).
- **Totem ledger.** Repository state is recorded in an append-only, hash-bound ledger at `pod/totems/`. Earlier entries are never rewritten. See [`AGENTS.md`](AGENTS.md).
- **Honest output.** In the Pod design, the interface changes only when the repair engine reports a real completed action, and the plan contract has no slot for a non-AI plan presented as an AI plan.

## Repository layout

| Path | Contents |
|---|---|
| `StoreApp/` | The Microsoft Store app (C#, WinUI 3, .NET 8) and its packaging project |
| `pod/` | Pod architecture: Orb and Portal contracts, the Totem ledger, current-state notes |
| `tests/` | Evaluation and smoke-test projects (PodEval, governance, model, integrity, UI) |
| `docs/` | Doctrine, certification evidence, preflight and release records |
| `CustodianPrototype/` | Early prototype of the Custodian interface |
| `index.html`, `privacy.html`, `terms.html` | Website pages (deployed with Vercel) |
| `certification_preflight.py`, `run-wack-*.ps1` | Certification preflight and Windows App Certification Kit scripts |

## Building

You'll need Visual Studio 2022 with the **.NET Desktop Development** and **Universal Windows Platform Development** workloads, and the .NET 8 SDK.

Build, packaging and Store submission steps are in [`StoreApp/BUILD_INSTRUCTIONS.md`](StoreApp/BUILD_INSTRUCTIONS.md). The bundled model (`StoreApp/Assets/models/gemma-3-1b-it/`, about 700 MB) has to be in place for the package to include on-device AI.

## Testing

- **PodEval** and the other evaluation projects under `tests/` are .NET console projects. Run one with `dotnet run --project tests/PodEval`.
- Python tests run with `pytest` from the repository root.
- To check the Totem ledger, run `python pod/totems/verify_totems.py`.

## Current version

1.2.26.0
