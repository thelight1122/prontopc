using System;
using System.IO;
using ProntoPC.Governance;

namespace ProntoPC;

// ── PipelineGovernance ────────────────────────────────────────────────────────
// The bridge between the UI admission seam (IPeerGovernance, Governance.cs) and the
// AEGIS governance module set (ProntoPC.Governance.*). This is the ONE place the two
// lanes join. Building it does NOT open the peer-facing path: it is gated CLOSED by an
// explicit master switch (`enabled`) that defaults false, AND additionally requires the
// exact authorized Canon to be present. Nothing else can open the Technician.
//
// While closed (the shipped default): OfferingAllowed is false, so the entry stays
// hidden, ScreenInbound refuses everything, the pipeline and Canon are never even
// touched (the `enabled` check short-circuits first — no Canon I/O, no ledger), and the
// on-device model is never reached. Behaviour is identical to AbsentGovernance.
//
// Contract mapping (seam -> pipeline), for when it IS opened:
//   SystemPrompt    -> the in-model reflective-posture seed (empty while gated)
//   OfferingAllowed -> enabled (the Canon is the model's embedded foundation — in-model,
//                      so there is nothing external to verify; the §8 switch is the gate)
//   ScreenInbound   -> closed: refuse. Open: PIM PrepareInbound, buffer the observations,
//                      ADMIT (return null) — ForceArtifactScreen observes without discarding;
//                      a blocking policy is a PIM-owner (Codex) decision (see Totem C2).
//   ApplyBoundary   -> pipeline.FinalizeOutbound(...): outbound PIM + Integrity + RBC/Prism,
//                      and writes the atomic hash-chained interaction-ledger record. Returns
//                      the peer-facing (Prism) text.
//   LogTurn         -> no-op: FinalizeOutbound already logs the whole turn atomically, so
//                      per-message logging would double-write and break the hash chain.
sealed class PipelineGovernance : IPeerGovernance
{
    // The master switch. FALSE ships. Opening the Technician is setting this true (Tracey's
    // §8 greenlight) AND having the exact Canon present — nothing else flips it.
    readonly bool _enabled;
    readonly CanonReference _canon;
    readonly string _ledgerPath;
    readonly string _sessionId = Guid.NewGuid().ToString("N");
    readonly string _model;

    CanonSeedResult? _seed;                        // cached exact-Canon load (only when enabled)
    CustodianGovernancePipeline? _pipeline;        // built lazily only on the open path
    (string text, PimScreenResult pim)? _pending;  // inbound buffered between Screen/Apply

    public PipelineGovernance(bool enabled = false, CanonReference? canon = null,
        string? ledgerPath = null, string model = "local-model")
    {
        _enabled = enabled;
        _canon = canon ?? CanonBoundarySeed.ProntoPcCanonV1;
        _ledgerPath = ledgerPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProntoPC", "interaction-ledger.jsonl");
        _model = model;
    }

    public const string ClosedReason =
        "The conversational Technician is pending its AEGIS safety approval and is not open yet. "
      + "Your repair itself is unaffected.";

    CanonSeedResult Seed => _seed ??= CanonBoundarySeed.Resolve(_canon);

    // Empty while gated (matches the seam contract).
    public string SystemPrompt => OfferingAllowed(out _) ? (Seed.SystemPrompt ?? "") : "";

    public bool OfferingAllowed(out string reason)
    {
        // The §8 master switch is the gate. The Canon is the model's embedded foundation —
        // what the model stands on, not an external source — so there is nothing further to
        // verify or load: when the switch is open and the model is present, the Canon is too.
        if (!_enabled) { reason = ClosedReason; return false; }
        reason = "";
        return true;
    }

    public string? ScreenInbound(string userText)
    {
        // Defense in depth: while closed, admit nothing (a non-null reason blocks).
        if (!OfferingAllowed(out var reason)) return reason;
        // PIM observes without discarding; buffer the observations for the atomic ledger
        // write in ApplyBoundary, and ADMIT. (Whether any inbound kind should BLOCK is a
        // PIM-owner decision — see Totem instruction C2.)
        PimScreenResult pim = Pipeline().PrepareInbound(userText);
        _pending = (userText, pim);
        return null;
    }

    public string ApplyBoundary(string response)
    {
        // Meaningful only on the open path. Without a buffered inbound, pass through unchanged.
        if (!OfferingAllowed(out _) || _pending is null) return response;
        (string peerText, PimScreenResult pim) = _pending.Value;
        _pending = null;
        GovernedReply reply = Pipeline().FinalizeOutbound(_sessionId, _model, peerText, response, pim);
        return reply.PeerFacingText;
    }

    // No-op by design: FinalizeOutbound writes the single atomic, hash-chained turn record.
    public void LogTurn(string role, string text) { }

    CustodianGovernancePipeline Pipeline() =>
        _pipeline ??= new CustodianGovernancePipeline(new AppendOnlyInteractionLedger(_ledgerPath), _canon);
}
