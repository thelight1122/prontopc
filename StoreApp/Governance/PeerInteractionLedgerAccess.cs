using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProntoPC.Governance;

// Per-Windows-user local location. The ledger has no automatic expiry or upload path;
// a later UI-owned surface can use this verified read API to display/export a Peer session.
public static class InteractionLedgerLocation
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProntoPC", "interaction-ledger.jsonl");
    public const string Retention = "local append-only; no automatic expiry or upload";
}

public sealed class PeerInteractionLedgerAccess
{
    readonly AppendOnlyInteractionLedger _ledger;

    public PeerInteractionLedgerAccess(string? path = null) =>
        _ledger = new AppendOnlyInteractionLedger(path ?? InteractionLedgerLocation.DefaultPath);

    public IReadOnlyList<InteractionLedgerRecord> ReadVerifiedSession(string sessionId) =>
        _ledger.ReadAndVerify().Where(record => string.Equals(record.SessionId, sessionId, StringComparison.Ordinal)).ToArray();
}
