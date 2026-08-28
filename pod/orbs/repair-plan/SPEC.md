# Orb Spec — `prontopc-repair-plan`

**Status:** draft (spec-only; not yet built)
**Kills:** Manifest specs **I2** (silent non-AI fallback) and **C3** (AI plan genuinely present)
**Required for full function:** yes
**Depends on:** `prontopc-llm-portal` (Azure→Gemini), optional `local-model-satellite`;
its emitted action IDs MUST be a subset of the `prontopc-repair-engine` catalogue.

## Why this Orb exists

The 4th certification rejection was "repair plan built by AI — absent." The cause was not a
missing feature; it was a **false sign**: both LLM providers were down and the client silently
fell back to a hard-coded heuristic plan (`StoreApp/MainWindow.xaml.cs` `BuildLocalPlan`),
presenting it through the same path as a real AI plan. The reviewer, correctly, saw no AI plan.

This Orb makes that failure **structurally impossible**, not merely unlikely. The fix is in the
*contract*, not in discipline: the Umbilical `output_contract` has exactly two shapes —
a genuine plan, or an explicit `unavailable` — and **no slot** for a heuristic plan wearing the
AI plan's clothes. If the plan cannot be built by a model, the only truthful thing the Orb can
emit is `unavailable`.

## Behaviour

1. Input: real diagnostics + session token.
2. Build the prompt from the diagnostics; the model may only select actions from the
   `prontopc-repair-engine` catalogue (the same allow-list handed to the LLM today), so plan and
   engine can never drift — every proposed action is one the engine can actually execute.
3. Provider order: `local-model-satellite` (if attached) → Azure OpenAI → Gemini, via
   `prontopc-llm-portal`. A plan from the local model is still a genuine AI plan — honestly
   labelled in `provenance.provider` — because a local model IS a model. This is where the
   embedded small multimodal model plugs in and removes the billing-cliff failure mode.
4. On a genuine plan → `status: "ok"` with `provenance` (provider + model) attached.
5. On all providers failing → `status: "unavailable"` with a reason. **Never** a heuristic plan
   under `status: "ok"`.

## Orchestrator obligation (attach, don't affect)

The client/orchestrator calls the Orb across its Umbilical and honours `status`:
- `ok` → present `plan`.
- `unavailable` → either block with an honest message, or present a clearly-labelled
  **"Offline basic plan (AI unavailable)"** — which is a *different, honestly-named* thing, not
  the AI plan. `BuildLocalPlan` may survive ONLY behind that explicit label, or be removed.

The plan-building logic leaves the client entirely and lives behind this Umbilical, so honouring
the contract requires no change to the engine or the stage — attach, don't affect.

## Definition of Done

**→ provisional** (one Auditor pass) when all hold:
- **C3:** `/api/repair-plan` returns a plan that references the supplied diagnostics and is NOT
  the canned-fallback text (`certification_preflight.check_ai_repair_plan` PASS).
- **I2:** an Auditor read of the shipping client finds no path that presents a non-AI plan as the
  AI plan; provider failure yields `unavailable` or an explicitly-labelled offline plan.
- **Catalogue integrity:** every `action.id` any plan can emit ∈ `prontopc-repair-engine` catalogue.
- **Provenance:** every `ok` plan carries `provenance.provider` + `provenance.model`.

**→ verified** additionally requires the 3/5 independent-reviewer marker promotion (builder excluded).

## Contracts in this folder
- `orb.json` — the Orb contract (this target).
- `umbilical.json` — the attachment; its `output_contract` is the structural I2 kill.
