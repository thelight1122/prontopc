namespace ProntoPC;

// ── AEGIS pre-offering admission gate (PRONTOPC_AEGIS_INTEGRATION_CONTRACT.md §8) ──
//
// The conversational Custodian may generate a peer-facing response ONLY once the
// self-governance modules exist and pass: the Canon seed (the model's platform of
// reflection), PIM (the inbound + outbound force-artifact screen), the Seven-Virtue
// Integrity screen, RBC / Reflective Boundary Conditions (the Prism, outbound), and the
// append-only, Peer-inspectable interaction log. These are the declared build-gaps
// (§5). Until they are built and wired, the gate is CLOSED: the Technician conversation
// exists in code and UI but is never offered to a customer and never generates a reply.
//
// This interface is the single seam the conversation runs through. The governance module
// set (built separately) supplies an OPEN implementation; swapping AbsentGovernance for
// it — and nothing else in the UI — turns the Technician on, safely.
interface IPeerGovernance
{
    // The in-model reflective-posture seed for the system prompt. The Canon is not carried
    // or referenced here — it is the model's embedded foundation, the ground it stands on —
    // so this only orients reflection (non-force, describe-and-offer). Empty while gated.
    string SystemPrompt { get; }

    // The §8 admission gate. False until every governance module is present and passing;
    // `reason` is a plain, honest explanation shown to the customer / used to hide entry.
    bool OfferingAllowed(out string reason);

    // PIM inbound: screen the customer's text for force artifacts / injection. Return null
    // to ADMIT, or a refusal reason to BLOCK before it ever reaches the model.
    string? ScreenInbound(string userText);

    // RBC / Prism outbound: apply Reflective Boundary Conditions to a generated response
    // (non-coercive, proportional, optional, free of coercive certainty / authority leak).
    string ApplyBoundary(string response);

    // The append-only, Peer-inspectable interaction log (distinct from the Totem dev ledger).
    void LogTurn(string role, string text);
}

// Default until the governance modules land: CLOSED. Admits nothing, seeds nothing,
// logs nothing. With this in place the Technician can never speak — which is the point:
// no ungoverned peer-facing generation ships.
sealed class AbsentGovernance : IPeerGovernance
{
    public const string GateReason =
        "The conversational Technician is pending its AEGIS safety review. Its self-governance "
      + "modules — the Canon seed, the injection/force-artifact screen (PIM), the Seven-Virtue "
      + "Integrity screen, the Reflective Boundary Conditions (RBC/Prism), and the append-only "
      + "interaction log — are not yet in place, so the Custodian will not generate replies until "
      + "they are. Your repair itself is unaffected.";

    public string SystemPrompt => "";
    public bool OfferingAllowed(out string reason) { reason = GateReason; return false; }
    // While gated, admit nothing — a non-null reason blocks every inbound message.
    public string? ScreenInbound(string userText) => GateReason;
    public string ApplyBoundary(string response) => response;
    public void LogTurn(string role, string text) { }
}
