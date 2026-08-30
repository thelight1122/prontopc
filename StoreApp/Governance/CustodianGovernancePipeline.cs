using System;

namespace ProntoPC.Governance;

// Future CustodianChat integration point. This class is deliberately independent of the
// UI and model runtime: callers prepare inbound text, obtain a model response, then finalize it.
public sealed class CustodianGovernancePipeline
{
    readonly AppendOnlyInteractionLedger _ledger;
    readonly CanonReference _canon;

    public CustodianGovernancePipeline(AppendOnlyInteractionLedger ledger, CanonReference? canon = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _canon = canon ?? CanonBoundarySeed.ProntoPcCanonV1;
    }

    public PimScreenResult PrepareInbound(string peerText) => ForceArtifactScreen.Screen(peerText, ScreenDirection.Inbound);

    public GovernedReply FinalizeOutbound(string sessionId, string model, string peerText, string rawModelResponse, PimScreenResult inboundPim)
    {
        PimScreenResult outbound = ForceArtifactScreen.Screen(rawModelResponse, ScreenDirection.Outbound);
        IntegrityScreenResult integrity = SevenVirtueScreen.Evaluate(rawModelResponse);
        PrismResult prism = ReflectiveBoundaryPrism.Apply(rawModelResponse, outbound.Observations);
        InteractionLedgerRecord record = _ledger.Append(sessionId, model, _canon.Sha256, peerText, rawModelResponse,
            prism.EmittedText, inboundPim, outbound, integrity, prism);
        return new GovernedReply(prism.EmittedText, outbound, integrity, prism, record.RecordId);
    }
}
