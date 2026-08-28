# ProntoPC Pod

A clean Pod, opened from the Manifest-of-Completion audit. Built with the AEGIS POD
Architecture (`H:\AEGIS-POD-ARCHITECTURE`), applied to a PC-repair product rather than a
CyberPeer DataQuad — so it carries *ProntoPC's* requirements (a thin orchestration core,
Orbs, Portals), not the CyberPeer's (PEER/PCT/NCT/SPINE tensors, steward/advocate,
temporal daemon). The Orb and Umbilical contracts are the real ones, copied verbatim into
`contracts/`, so every Orb here is ecosystem-compatible.

## The one rule this repo exists to keep

**Attach, don't affect.** A capability connects to the Pod through an Umbilical; adding or
removing it never edits the Pod core. That is why the audit's blocking failures don't get
*patched* — they cease to exist:

- **I1 (three drifting engines)** → one `prontopc-repair-engine` Orb. The PowerShell-web and
  Python implementations stop being parallel engines; if kept at all they become a Portal's
  concern, not a second copy of the logic.
- **I2 (silent non-AI fallback)** → the `prontopc-repair-plan` Orb's Umbilical `output_contract`
  is `{plan}` **or** `{status:"unavailable"}`. There is no code path that can present a
  non-AI plan as the AI plan, because the contract has no slot for it.
- **U1–U6 (UI is not the semiotics)** → the `prontopc-custodian-stage` Orb subscribes to the
  repair-engine Umbilical's `observation_contract`. A scene can only change on a real
  `action.completed` event — a sign literally cannot fire without its referent.

## Layers here

| Layer | Meaning | In this Pod |
|---|---|---|
| **Orb** | capability working on data the Pod owns (inside) | repair-engine, diagnostics, repair-plan, custodian-stage, session, loyalty |
| **Portal** | an Orb whose function crosses outward to a service the Pod doesn't own | stripe, llm, cloudrun, mint |
| **Satellite** | serves one Orb internally, one level down | *planned:* local-model Satellite serving repair-plan (the embedded small multimodal model) |
| **Umbilical** | the attachment contract (input/output/observation/evidence + working_parameters) | one per Orb/Portal |

## Trust gate — "Done" is a verdict, not an opinion

Every attachment declares `trust_state` (draft → provisional → verified) and the
`manifest_specs` whose PASS promotes it. An Orb reaches **verified** only when its Manifest
specs pass under the Auditor AND the full 3/5 independent-reviewer marker promotion (builder
excluded) is met — a single Auditor pass earns **provisional**, no more. The Pod is "Done"
when every `required_for_full_function` attachment is verified.

Current state (from the opening audit — 5 PASS / 3 FAIL / 4 PARTIAL / 4 UNVERIFIABLE):

| Attachment | trust | placed / new | specs |
|---|---|---|---|
| repair-engine | provisional | placed (`Repair.cs`) | I1, I4, C4 |
| diagnostics | provisional | placed (`Diagnostics.cs`) | C4 |
| session | provisional | placed | C4 |
| loyalty | provisional | placed | — |
| stripe-portal | provisional | placed | C1, I5 |
| cloudrun-portal | provisional | placed | C2 |
| repair-plan | draft | **create clean** (kills I2) | I2, C3 |
| custodian-stage | draft | **create clean** (kills U1–U6) | U1–U6 |
| llm-portal | draft | **build resilient** (I3) | C3, I3 |
| mint-portal | draft | **create** | — |

Open items the audit flagged that a specific Orb now owns:
- **C4 false claim — RESOLVED (TOTEM-0012):** the listing claim was corrected to the honest count.
  `docs/custodian-spec.md` now says "25 system areas" (matching the 25 `Section(...)` collectors in
  `Diagnostics.cs`) instead of "35+". Alternative path (grow `diagnostics` to 35 real categories) not taken.
- **I3**: `llm-portal` must prove primary+fallback resilience; the local-model Satellite is the
  durable answer (no billing cliff).

## Files
- `contracts/prontopc-pod.schema.json` — ProntoPC's Pod design contract (its requirements).
- `contracts/orb.schema.json`, `contracts/umbilical.schema.json` — canonical AEGIS contracts, verbatim.
- `prontopc.pod.json` — the Pod manifest (this composition).
- `orbs/<name>/orb.json` + `umbilical.json` — each Orb and its attachment. `repair-engine` is the built exemplar.
