# Orchestrator Spec — the Pod processor (`prontopc` client)

**Status:** draft (Stage 1 in build). **Owns:** phase/session state ONLY — no diagnostic or
repair logic (pod.json invariant). **Holds:** the Action Envelope, and **keys** the repair-plan
Umbilical to it (pod.json `orchestrator.holds_contracts`).

## Why this exists

Today `StoreApp/MainWindow.xaml.cs` (275 lines) *is* the orchestrator, inline: it collects
diagnostics, POSTs `/api/submit-diagnostics`, branches on `status`, renders findings, then loops
`Repair.Execute`. The phases are implicit in UI panels; plan authoring is delegated to the cloud
with no envelope enforcement on the client. This spec extracts the sequencing into a real
Orchestrator and makes it the single point that **keys the plan Umbilical to the envelope** —
attach, don't affect: `Diagnostics`, `Repair`, and the XAML panels are untouched.

## Phase contract (greet → scan → plan → consent → repair → verify → complete)

| Phase | Orchestrator does | Orb / Umbilical crossed |
|---|---|---|
| greet | show intro; establish session token | — |
| scan | `Diagnostics.Collect()` | diagnostics Orb (in-process) |
| plan | **key** the plan Umbilical: hash-check `envelope_ref`; compile the per-machine **admissible set** (envelope precondition × diagnostics); ask the attached author for a plan restricted to it; **validate** the returned plan against the envelope | repair-plan Orb (Stage 1: cloud author; Stage 2: local-model Satellite first) |
| consent | render each action's `risk` / `requires_restart` / `estimated_mb` **from the envelope**, not from model text; user unchecks | — |
| repair | loop `Repair.Execute` over approved+admissible actions | repair-engine Orb (in-process) |
| verify | `Repair.NeedsRestart`; summarize done/failed | repair-engine Orb |
| complete | POST results; thank-you / coupon | session/loyalty |

The keyed envelope is the **guard on `plan → consent`**: no plan advances until it validates.

## Staged build

### Stage 1 — Orchestrator + envelope keying (NO new ML dependency) ← in build
- **U1 `Envelope.cs`** (additive; new file, wired last). Loads the bundled `action-envelope.json`,
  evaluates the closed predicate op-set over a diagnostics dictionary, exposes:
  - `AdmissibleSet(diag) : ISet<string>` — ids whose precondition holds on this machine.
  - `Validate(plan, diag) : (plan', dropped[])` — drop non-catalogue / false-precondition /
    missing-required-param actions; clamp `estimated_mb_recovered` to the `mb` bound; **stamp**
    `risk` + `requires_restart` from the envelope; enforce `max_actions` + subsumption.
  - `Sha256Upper()` / `Matches(refSha)` — for the Umbilical `envelope_ref` hash-key.
- **U2 `Orchestrator.cs`** — the phase/session state machine; invokes the Orbs; keys the plan
  Umbilical (U1). `MainWindow` becomes the thin view it drives.
- **U3** — wire the **existing cloud author** as the plan provider behind the Orchestrator; consent
  renders from the envelope. Reconcile envelope field paths against `Diagnostics.cs` keys (the
  backend `$diag` and the client dictionary must agree) — the eval fixtures below are the check.
- **DoD (→ Stage 1 done):** app builds 0 errors; a cloud plan is envelope-validated before consent
  (an out-of-catalogue or false-precondition action is dropped and logged); consent labels come
  from the envelope; all existing flows still work; ~10 diagnostics fixtures exercise
  `AdmissibleSet`/`Validate`. Append a Totem.

### Stage 2 — attach the `ILocalPlanner` (Gemma 3 1B Satellite)
- csproj: ONNX Runtime GenAI + DirectML; bundle Gemma 3 1B ONNX under `Assets/models/`.
- `GemmaLocalPlanner : ILocalPlanner` — lazy-load+warm; prompt from diagnostics; **grammar
  constrained to the Orchestrator's admissible set**; return `{plan, provenance:"local-model"}` or
  `null`. Provider order: local → cloud → labelled offline.
- Full eval harness (~20 fixtures); Gemma license bundled; offline-VM DoD (an `ok` plan with
  `provenance.provider == "local-model"` and no network). This is the durable C3 billing-cliff kill.

## The interface Stage 2 will implement (declared now so Stage 1 leaves the seam)

```csharp
interface IPlanAuthor {                    // both cloud and local implement this
    // Author a plan restricted to `admissible`. Return null to fall through to the next author.
    Task<PlanResult?> AuthorAsync(Dictionary<string, object?> diag, ISet<string> admissible, CancellationToken ct);
}
// PlanResult = { JsonElement plan; string provider; string model; }
// Orchestrator tries authors in provider_order; validates each result via Envelope.Validate
// before accepting; if all return null/invalid → labelled offline plan (BuildLocalPlan).
```

## Non-negotiables carried from the Pod
- Orchestrator owns state only — repair/diagnostic logic stays in `Repair`/`Diagnostics`.
- No plan reaches consent unvalidated; `risk`/`requires_restart`/mb come from the envelope.
- A non-AI plan is never shown as the AI plan (I2) — offline is explicitly labelled.
