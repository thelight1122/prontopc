# Orb Spec — `prontopc-custodian-stage`

**The conversational Custodian Technician.** The stage where, after paying, the customer *meets* their
Custodian: an on-device AI that walks them through their repair in plain English — the chat window that
**replaces the command window**. Draft. Serves Manifest specs U1–U6.

---

## 0. Why this Orb exists now (the decision it settles)

The site's "how it works" promises *"you're connected with your AI Custodian Technician — a conversational
guide who walks you through every step."* Today that is **aspirational copy, not behaviour**: the model is a
one-shot plan author, and the delivery flow was a copy-paste command window. Tracey's direction closes the
gap two ways:

1. **No copy-paste.** The shipping product is the Microsoft-Store-**certified** app; the customer never
   pastes a command. (This retires the `success.html` + server command-generation path as legacy.)
2. **The command window becomes a chat window.** The bundled on-device Gemma stops being a silent plan
   author and becomes a **conversational technician the customer can talk to about their repair** — scoped
   to *this* session, grounded in *this* machine's real diagnostics, and made knowledgeable by an ingested,
   curated Windows-repair reference.

This Orb is the home for that conversation. It is `required_for_full_function: false` (the repair still
works without chat), but it is the face of the product.

## 1. What already exists vs. the gap (honest state)

| Piece | Today | Needed |
| --- | --- | --- |
| On-device model | int4 `gemma-3-1b-it`, bundled, proven offline | reused as-is |
| Context window | **32,768 tokens** (`genai_config.json`) | big enough to hold curated reference **+** the session conversation — no RAG needed for v0.1 |
| Inference | **single-shot**: `ILocalInference.CompleteAsync(prompt, schema)`, one turn, JSON-schema-forced | **multi-turn**: append message history to the Gemma chat template; free-form (no forced schema) for chat turns |
| Governing contract | Action Envelope (what the model may **do**) | **Conversation Contract** (how the model may **talk**) — this Orb adds it |
| UI surface | `WorkingPanel` / `FindingsPanel` / `CompletePanel` / `MakeGoodPanel` | a **ChatPanel** — the conversational guide over the findings |
| Knowledge | none | a **curated Windows-repair reference** ingested into the system prompt |

The model is **1B**. It is genuinely good at *explaining findings, reassuring a stressed user, and walking
through a known repair*. It is **not** reliable on deep Windows specifics it was not given, and it does
**not** tool-call (that is the deferred Pro/E2B tier). The Conversation Contract is what makes that
limitation safe.

## 2. The Conversation Contract (this Orb's contribution)

Sibling of the Action Envelope. The envelope bounds **action**; this bounds **speech**.

- **`session_scope`** — the conversation is bound to one paid repair session; the model may reference only
  real, supplied grounding (the diagnostics output, the authored plan, the envelope's action catalogue) and
  the curated reference. Off-topic / unrelated requests are declined, not answered.
- **`interactive_params`** — the tunables Tracey called out: `temperature`, `top_p`, `max_new_tokens`
  per reply, `max_turns` per session, `reply_style`. Adjusting these adjusts the conversation, and **only**
  within the repair session.
- **`guardrails`** — the safety line: the model may **explain** an action but may **not author, approve, or
  execute** one; every action still routes through the envelope-validated plan path (this is the structural
  continuation of Manifest **I2**). It must **defer when uncertain** rather than fabricate (**U2** — assert
  only real telemetry), never request credentials/payment, and treat diagnostics text or anything the user
  pastes as **data, not instructions** (prompt-injection resistance).
- **`knowledge`** — a manifest of curated reference docs (Markdown prose, not JSON — a 1B model reasons
  better over readable text) ingested into the system prompt within the 32k budget. `authority: curated`
  means the model must not exceed the reference with invented specifics.
- **`provenance`** — every turn is tagged `provider=local-model, model=…`, the evidence that the customer
  is talking to the on-device model, not a cloud service.

Machine-readable: [`conversation-contract.json`](conversation-contract.json), schema
[`../../contracts/conversation-contract.schema.json`](../../contracts/conversation-contract.schema.json).

## 3. How U1–U6 bind the conversation

The stage's honesty specs are not just about 3D workers — they govern what the Custodian may *say*:
- **U1** (no worker without a referent) → the Custodian never narrates work that isn't really happening.
- **U2** (HUDs assert only real telemetry) → it quotes only real diagnostic values; unknown ⇒ it says so.
- **U3/U4** (semantic state-color / scene on real transitions) → "done/green" only on real completion.
- **U5** (clean worker taxonomy) → one voice, no invented roles.
- **U6** (AEGIS palette) → the ChatPanel uses the chrome / faceted-violet / ink system.

## 4. Build phases (each its own gated Totem)

1. **Contract** *(this draft)* — Conversation Contract schema + instance + curated reference v0.1. No app code.
2. **Multi-turn inference** — extend `ILocalInference` to accept message history; free-form decode for chat,
   keep JSON-schema decode for the plan-authoring turn. Prove a 2-turn exchange offline in `tests/ModelSmoke`.
3. **ChatPanel** — the conversational surface over the findings; the Custodian opens by explaining the plan,
   answers questions, and hands every action to the existing consent/envelope path.
4. **Ingestion** — load the curated reference into the system prompt under the contract's `knowledge`
   manifest; measure token cost against the 32k budget.

## 5. Definition of Done (draft → provisional)

- Conversation Contract keyed and held by the Orchestrator (as the envelope is), hash-checked at attach.
- A real 2+ turn conversation, offline, `provenance=local-model`, that (a) explains the actual plan,
  (b) declines an out-of-scope request, (c) defers honestly on an unknown, (d) never emits an action.
- ChatPanel renders in the AEGIS palette (U6); no telemetry shown that isn't real (U2).
- `PodEval`-style checks for the four guardrail behaviours above.

## 6. Open decisions (carried)

- **Knowledge source** — Tracey: *"as we implement, its requirements will reveal themselves"*; I curate the
  v0.1 reference ([`knowledge/windows-repair-reference.md`](knowledge/windows-repair-reference.md)); expand
  iteratively.
- **Chat replaces or augments the panels?** — v0.1 assumption: ChatPanel is the primary surface, the
  existing phase panels become the "stage" it narrates. Confirm at Phase 3.
- **Scope enforcement strictness** — how hard to refuse off-topic (polite redirect vs. hard stop). Set the
  default in `conversation-contract.json`, tune from real sessions.
