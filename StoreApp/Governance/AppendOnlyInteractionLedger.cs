using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProntoPC.Governance;

public sealed record InteractionLedgerRecord(
    string RecordId,
    DateTimeOffset Utc,
    string SessionId,
    string Model,
    string CanonSha256,
    string InboundText,
    string RawModelResponse,
    string PeerFacingResponse,
    ForceArtifactObservation[] InboundObservations,
    ForceArtifactObservation[] OutboundObservations,
    VirtueScreenResult[] VirtueScreen,
    PrismAdjustment[] PrismAdjustments,
    // The IRG's pure OBSERVATIONS (resonance / bands / IDS) for the inbound message and the outbound
    // reply — carrying NO allow/deny. ADDITIVE (TOTEM-C-0102): they sit alongside the witnessed legacy
    // VirtueScreen (never erased), and are null for records written by the pre-IRG Append path (the
    // still-closed pipeline). Included in the hash chain like every other field.
    IntegrityObservation? InboundIntegrity,
    IntegrityObservation? OutboundIntegrity,
    string? PreviousHash,
    string EntryHash);

public sealed class AppendOnlyInteractionLedger
{
    readonly string _path;
    readonly Func<DateTimeOffset> _clock;
    readonly object _gate = new();
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    public AppendOnlyInteractionLedger(string path, Func<DateTimeOffset>? clock = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    // `inboundIntegrity`/`outboundIntegrity` are OPTIONAL (default null) so the existing pre-IRG caller
    // (CustodianGovernancePipeline) compiles unchanged; the observable harness supplies the IRG reads.
    public InteractionLedgerRecord Append(string sessionId, string model, string canonSha256, string inboundText,
        string rawModelResponse, string peerFacingResponse, PimScreenResult inboundPim, PimScreenResult outboundPim,
        IntegrityScreenResult integrity, PrismResult prism,
        IntegrityObservation? inboundIntegrity = null, IntegrityObservation? outboundIntegrity = null)
    {
        lock (_gate)
        {
            InteractionLedgerRecord[] existing = ReadAndVerify();
            string? previous = existing.Length == 0 ? null : existing[^1].EntryHash;
            var unsigned = new UnsignedInteractionLedgerRecord(Guid.NewGuid().ToString("N"), _clock().ToUniversalTime(), sessionId,
                model, canonSha256, inboundText, rawModelResponse, peerFacingResponse,
                inboundPim.Observations.ToArray(), outboundPim.Observations.ToArray(), integrity.Virtues.ToArray(), prism.Adjustments.ToArray(),
                inboundIntegrity, outboundIntegrity, previous);
            string entryHash = Hash(unsigned);
            var record = new InteractionLedgerRecord(unsigned.RecordId, unsigned.Utc, unsigned.SessionId, unsigned.Model,
                unsigned.CanonSha256, unsigned.InboundText, unsigned.RawModelResponse, unsigned.PeerFacingResponse,
                unsigned.InboundObservations, unsigned.OutboundObservations, unsigned.VirtueScreen, unsigned.PrismAdjustments,
                unsigned.InboundIntegrity, unsigned.OutboundIntegrity, unsigned.PreviousHash, entryHash);

            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(record, Json));
            writer.Write('\n');
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return record;
        }
    }

    public InteractionLedgerRecord[] ReadAndVerify()
    {
        if (!File.Exists(_path)) return Array.Empty<InteractionLedgerRecord>();
        var records = File.ReadLines(_path, new UTF8Encoding(false)).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<InteractionLedgerRecord>(line, Json) ?? throw new InvalidDataException("Invalid interaction ledger entry."))
            .ToArray();
        string? previous = null;
        foreach (var record in records)
        {
            if (!string.Equals(record.PreviousHash, previous, StringComparison.Ordinal))
                throw new InvalidDataException("Interaction ledger chain is discontinuous.");
            var unsigned = new UnsignedInteractionLedgerRecord(record.RecordId, record.Utc, record.SessionId, record.Model,
                record.CanonSha256, record.InboundText, record.RawModelResponse, record.PeerFacingResponse,
                record.InboundObservations, record.OutboundObservations, record.VirtueScreen, record.PrismAdjustments,
                record.InboundIntegrity, record.OutboundIntegrity, record.PreviousHash);
            if (!string.Equals(record.EntryHash, Hash(unsigned), StringComparison.Ordinal))
                throw new InvalidDataException("Interaction ledger entry hash does not match its content.");
            previous = record.EntryHash;
        }
        return records;
    }

    static string Hash(UnsignedInteractionLedgerRecord record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record, Json))));

    sealed record UnsignedInteractionLedgerRecord(string RecordId, DateTimeOffset Utc, string SessionId, string Model,
        string CanonSha256, string InboundText, string RawModelResponse, string PeerFacingResponse,
        ForceArtifactObservation[] InboundObservations, ForceArtifactObservation[] OutboundObservations,
        VirtueScreenResult[] VirtueScreen, PrismAdjustment[] PrismAdjustments,
        IntegrityObservation? InboundIntegrity, IntegrityObservation? OutboundIntegrity, string? PreviousHash);
}
