using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProntoPC.Governance;

// ── Append-only Technician → Communications relay ────────────────────────────────────────────
// The seam between the two Orbs, made an APPEND-ONLY, hash-bound message rather than a mutable call.
//
// The Computer Technician Orb (scan → on-device plan authoring) POSTS its findings here ONCE,
// immutably. The Communications & Governance Orb READS the latest findings for its session to talk
// about what is already true — it authors nothing and executes nothing (Manifest I2: actionable
// intent is a consent handoff, never a direct execution). Because the relay is a record, not a
// channel, this works with a SINGLE model: the Orbs coordinate through the append-only trail, not by
// conversing. Nothing here opens a gate or runs a model.
//
// WHY append-only (the AEGIS invariant): all AEGIS apps and standards are append-only — the
// structural form of non-force (nothing overwritten or silenced), transparency (tamper-evident,
// hash-chained, inspectable), and continuity (prior findings preserved; the latest is the current
// view). Sibling of AppendOnlyInteractionLedger; same pattern.
//
// HEAD ANCHOR (TOTEM-C-0110, closing the X-0078 gap): the intra-file hash chain catches a mid-chain
// edit or a malformed line, but NOT tail truncation or whole-ledger replacement by a shorter valid
// chain — those need an anchor held OUTSIDE the writable relay file. When an `anchorPath` is supplied
// (the caller places it in a SEPARATE failure domain from the relay), Append records {count, head}
// there and ReadAndVerify requires the relay's length + head to match it — so dropping the last N
// records or swapping the whole file is illuminated. The anchor is optional so the pure component
// stays usable without it.

// One recommended repair the Communications Orb may explain, and whose id it may route to the
// envelope-validated consent path (never execute itself).
public sealed record FindingsAction(string Id, string Title, int EstimatedMbRecovered);

public sealed record FindingsRelayRecord(
    string RecordId,
    DateTimeOffset Utc,
    string SessionId,
    string Provider,
    string Model,
    string PlanSummary,
    FindingsAction[] Actions,
    string DiagnosticsSha256,
    string? PreviousHash,
    string EntryHash);

public sealed class AppendOnlyFindingsRelay
{
    readonly string _path;
    readonly string? _anchorPath;
    readonly Func<DateTimeOffset> _clock;
    readonly object _gate = new();
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    // anchorPath: an OPTIONAL head anchor, which the caller MUST place in a failure domain separate
    // from `path` for it to illuminate tail-truncation / whole-file replacement (per TOTEM-X-0079).
    public AppendOnlyFindingsRelay(string path, Func<DateTimeOffset>? clock = null, string? anchorPath = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _anchorPath = anchorPath;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    // The Technician Orb posts its findings. Appends one immutable, hash-chained record and returns it.
    public FindingsRelayRecord Append(string sessionId, string provider, string model,
        string planSummary, IEnumerable<FindingsAction> actions, string diagnosticsSha256)
    {
        if (string.IsNullOrEmpty(sessionId)) throw new ArgumentException("sessionId required", nameof(sessionId));
        var actionArray = (actions ?? Enumerable.Empty<FindingsAction>()).ToArray();
        lock (_gate)
        {
            FindingsRelayRecord[] existing = ReadAndVerify();
            string? previous = existing.Length == 0 ? null : existing[^1].EntryHash;
            var unsigned = new UnsignedFindingsRelayRecord(Guid.NewGuid().ToString("N"), _clock().ToUniversalTime(),
                sessionId, provider ?? "", model ?? "", planSummary ?? "", actionArray, diagnosticsSha256 ?? "", previous);
            string entryHash = Hash(unsigned);
            var record = new FindingsRelayRecord(unsigned.RecordId, unsigned.Utc, unsigned.SessionId, unsigned.Provider,
                unsigned.Model, unsigned.PlanSummary, unsigned.Actions, unsigned.DiagnosticsSha256, unsigned.PreviousHash, entryHash);

            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(JsonSerializer.Serialize(record, Json));
                writer.Write('\n');
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            WriteAnchor(existing.Length + 1, entryHash);
            return record;
        }
    }

    // Verify the whole chain (continuity + per-entry hash) and, when an anchor is configured, the
    // head anchor (length + last hash). Throws on any discontinuity, altered content, malformed
    // line, or anchor mismatch — tamper (including tail truncation / whole replacement) is
    // illuminated, not silent.
    public FindingsRelayRecord[] ReadAndVerify()
    {
        var records = File.Exists(_path)
            ? File.ReadLines(_path, new UTF8Encoding(false)).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<FindingsRelayRecord>(line, Json) ?? throw new InvalidDataException("Invalid findings relay entry."))
                .ToArray()
            : Array.Empty<FindingsRelayRecord>();
        string? previous = null;
        foreach (var record in records)
        {
            if (!string.Equals(record.PreviousHash, previous, StringComparison.Ordinal))
                throw new InvalidDataException("Findings relay chain is discontinuous.");
            var unsigned = new UnsignedFindingsRelayRecord(record.RecordId, record.Utc, record.SessionId, record.Provider,
                record.Model, record.PlanSummary, record.Actions, record.DiagnosticsSha256, record.PreviousHash);
            if (!string.Equals(record.EntryHash, Hash(unsigned), StringComparison.Ordinal))
                throw new InvalidDataException("Findings relay entry hash does not match its content.");
            previous = record.EntryHash;
        }
        VerifyAnchor(records);
        return records;
    }

    // The Communications Orb reads the most recent verified findings for its session (or null).
    public FindingsRelayRecord? ReadLatestForSession(string sessionId) =>
        ReadAndVerify().LastOrDefault(r => string.Equals(r.SessionId, sessionId, StringComparison.Ordinal));

    // ── head anchor (separate failure domain) ──────────────────────────────────────────────────
    sealed record HeadAnchor(int Count, string? Head);

    void WriteAnchor(int count, string head)
    {
        if (_anchorPath is null) return;
        string? dir = Path.GetDirectoryName(_anchorPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_anchorPath, JsonSerializer.Serialize(new HeadAnchor(count, head), Json), new UTF8Encoding(false));
    }

    void VerifyAnchor(FindingsRelayRecord[] records)
    {
        if (_anchorPath is null) return;
        if (!File.Exists(_anchorPath))
        {
            if (records.Length == 0) return;                 // fresh relay, anchor not yet written
            throw new InvalidDataException("Findings relay anchor is missing while records exist.");
        }
        HeadAnchor? anchor = JsonSerializer.Deserialize<HeadAnchor>(File.ReadAllText(_anchorPath, new UTF8Encoding(false)), Json);
        if (anchor is null) throw new InvalidDataException("Findings relay anchor is unreadable.");
        string? head = records.Length == 0 ? null : records[^1].EntryHash;
        if (anchor.Count != records.Length || !string.Equals(anchor.Head, head, StringComparison.Ordinal))
            throw new InvalidDataException("Findings relay head anchor does not match the file (tail truncation or replacement).");
    }

    static string Hash(UnsignedFindingsRelayRecord record) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record, Json))));

    sealed record UnsignedFindingsRelayRecord(string RecordId, DateTimeOffset Utc, string SessionId,
        string Provider, string Model, string PlanSummary, FindingsAction[] Actions,
        string DiagnosticsSha256, string? PreviousHash);
}
