# Drift record — formed-model integration ahead of evidence (2026-09-03)

*Observer: Claude lane, session scratch-2026-09-03-96a761-a2. This is an append-only,
non-force **observation** — it describes what IS at this UTC, names the gap as a
formation focus (not a fault), and changes no in-flight file. Tracey's standing call
for this record: **record the drift for now; flag the cert impact; do not rebuild.***

## What was authorized, and what landed (TOTEM-X-0085, Codex, 2026-09-03T01:57:33Z)

Under Tracey's authorization to bring the latest educated run forward, Codex merged the
reversible responder adapter `gemma3-1b-formation-v0.2.1` (adapter sha256
`E604ACB2…`) into the frozen base (`google/gemma-3-1b-it`, snapshot `dcc83ea8…`) and
exported a CPU int4 ONNX Runtime GenAI artifact to
`StoreApp/Assets/models/gemma-3-1b-it-formed-v0.2.1/` (does **not** overwrite the base
`gemma-3-1b-it/`). X-0085 records, in its own words:

- a CPU ORT model-open / tokenize check holds;
- **no uncapped behavioral certification eval was performed** — the bounded 8-token raw
  probe returned `"ProntoPC is read-write"` (a non-plan string);
- the artifact is **"operationally exported but not release-certified"**;
- **"no application loader/package selection changed"**, and its own next step is to
  *"integrate … through the app loader and packaging **only after an appropriate
  behavioral evaluation**."*

## The drift observed now (this session, ~2026-09-03T01:5x UTC)

An integration is **in progress** but sits as **uncommitted, un-Totemed drift**, and the
POD manifest has been moved **ahead of the evidence**.

**1. Loader + packaging are being wired (loose drift, no Totem binds them yet).**
`python pod/totems/verify_totems.py` reports **7 files in DRIFT**, none re-bound:

| File | Bound authority | Observed change |
|------|-----------------|-----------------|
| `StoreApp/LocalPlanner.cs` | TOTEM-C-0071 | adds a `ProductModel` selector (`DirectoryPath`/`Name`) |
| `StoreApp/MainWindow.xaml.cs` | TOTEM-C-0112 | loader now `CreateOrAbsent(ProductModel.DirectoryPath, ProductModel.Name)` for plan-author (:58/:62) and Custodian (:536/:541) — no longer the literal `gemma-3-1b-it` |
| `StoreApp/ProntoPC.csproj` | TOTEM-C-0097 | bundles `Assets\models\gemma-3-1b-it-formed-v0.2.1\**` (was base only) |
| `StoreApp/build.ps1` | TOTEM-C-0097 | model-prune/copy updated for the formed dir |
| `tests/ModelSmoke/ModelSmoke.csproj` | TOTEM-C-0071 | packaging update |
| `tests/ModelSmoke/Program.cs` | TOTEM-C-0071 | model-selection update |
| `pod/prontopc.pod.json` | TOTEM-C-0119 | manifest `model` block rewritten (see #2) |

None of these is committed; `git status` shows all 8 (incl. `totems.codex.jsonl`) as
modified working-tree files. The verifier is therefore **RED** on this tree.

**2. The manifest claims a certified-sounding integration the evidence does not yet
support — and contradicts itself.** `pod/prontopc.pod.json` (edited after TOTEM-C-0119)
now states the formed model is *"selected as the Product plan-author"*
(`canon_layer.status: "formed-and-integrated-pending-product-acceptance"`) and a Nature
Layer is *"packaged and supplied"* — while, in the **same file**, `seed.note` still
reads *"not yet, since the shipped plan-author is base."* The overclaim is unverified on
four counts: (a) the change is uncommitted / un-Totemed drift; (b) behaviorally
unverified — X-0085 is explicitly *not release-certified* and the only probe returned a
non-plan string; (c) no Product acceptance evaluation exists; (d) the SEED remains
`pending`. This is the "sounding aligned ≠ being aligned" pattern (Canon Addendum A7):
the documentation is running ahead of the code and the evaluation.

**3. Behavioral gap (the real formation focus).** There is **no uncapped plan-authoring
evaluation** of the formed CPU int4 export. A formed-disposition model swapped in as the
plan-author could author better, equal, or *worse* structured plans than base; nothing
on record shows which. The proper gate (X-0085's own next step) is a ModelSmoke run
against `gemma-3-1b-it-formed-v0.2.1` proving `provider==local-model`, ≥1
Action-Envelope-admissible action, and a real (non-degenerate) summary.

**4. Nature Layer.** A **Product** Nature Layer file exists at
`pod/orbs/repair-plan/satellites/local-model/education/nature-layer/repair-nature-layer.json`
(a bounded evidence-domain map: `BOUNDARY`, `aspects`, `coverage_invariant` — **not** a
CyberPeer Nature Layer) and appears in the Release build output at
`…/Assets/aegis/repair-nature-layer.json`. Its committed source placement under
`StoreApp/Assets/aegis/` and its runtime wiring are part of the same un-Totemed drift;
the manifest already narrates it as packaged-and-supplied.

## Cert consequence (flagged, not acted — Tracey's call)

If the formed model becomes the **live plan-author**, the base-model recert package
**MSIX v1.2.0.2** (TOTEM-C-0097) is invalidated: it bundles base `gemma-3-1b-it`, so a
recert would have to be **rebuilt** with `gemma-3-1b-it-formed-v0.2.1` bundled and
preflight re-run. Per Tracey: **flag it, do not rebuild.** No package was rebuilt and no
submission touched by this record.

## Boundaries honored by this record

No in-flight file was modified or reverted; the manifest was **not** reverted (it is
another lane's current working state); nothing was wired, built, packaged, or deployed by
this record; `PipelineGovernance` stays `enabled:false`; the gate is unchanged.

## Suggested resolution (for a future authorized session — IDS, not a verdict)

- **Identify:** the integration is real but unverified and un-Totemed; the manifest leads
  the evidence.
- **Define:** "integrated" should mean *loader + packaging wired **and** a behavioral eval
  passed **and** one Totem binds every touched file **and** the manifest's `seed`/base
  language reconciled.*
- **Suggest:** run ModelSmoke against the formed export; **if** it authors valid plans,
  let the change land under a single Totem that hash-binds all touched files, reconciles
  the `seed` note, and issues the SEED; **if** it regresses vs base, hold the swap and keep
  base as the shipped plan-author. Either way, the recert rebuild stays Tracey's external
  track.
