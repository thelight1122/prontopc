namespace ProntoPC.Governance;

// AEGIS Build Invariants §5: Default Allow / Silent Admit. Inbound PIM makes a
// descriptive observation and preserves the Peer’s text; it does not decide for the Peer.
public static class InboundPimPolicy
{
    public const string PolicyId = "observe-and-admit";

    public static PimScreenResult ObserveAndAdmit(string peerText) =>
        ForceArtifactScreen.Screen(peerText, ScreenDirection.Inbound);
}
