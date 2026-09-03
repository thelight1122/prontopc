using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProntoPC.Governance;

// ── The versioned, hash-bound runtime Totem Reference Library ─────────────────────────────────
// The general technical repair knowledge the Model is grounded on, made an AUDITABLE, tamper-evident
// record on the SAME append-only discipline as the findings relay (TOTEM-X-0084 vocabulary: the
// "runtime Totem Reference Library"). Where the per-repair findings are a session record on the
// relay, this is the STABLE, versioned reference both Orbs share.
//
// The library ships as a versioned document; this component loads it and verifies its content
// against the pinned (version, sha256) so the Model never grounds on an ALTERED or UNVERSIONED
// reference. A content mismatch or an absent file is ILLUMINATED as null — the Model grounds on a
// verified reference or on none, never on an unversioned document, and there is no silent fallback.
// Versions SUPERSEDE (append-only): a new library is a new (version, sha) pin, the prior one is not
// overwritten. Read-only; runs no model; opens no gate; executes nothing.
//
// The hash is taken over the CRLF-normalized (LF) bytes so the pin is stable across line-ending
// differences between checkouts.

public sealed record TotemReference(string Version, string Sha256, string Content);

public sealed class TotemReferenceLibrary
{
    readonly string _path;
    readonly string _version;
    readonly string _pinnedSha256;   // uppercase hex over the LF-normalized content

    public TotemReferenceLibrary(string path, string version, string pinnedSha256)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _version = version ?? "";
        _pinnedSha256 = (pinnedSha256 ?? "").ToUpperInvariant();
    }

    // The curated Windows repair reference for this build. Pins the version + content hash so a
    // repointed or edited library is illuminated (null) rather than silently grounding the Model.
    // The path is supplied by the caller (a shipped Assets path at runtime; the pod knowledge file
    // in the eval), so the same pin verifies wherever the document actually lives.
    public static TotemReferenceLibrary WindowsRepairReference(string path) =>
        new TotemReferenceLibrary(path,
            version: "windows-repair-reference/v0.1",
            pinnedSha256: "65D0DA317FE7DCD90AEE109A96F61E586C5424430274A8D2801C91A416E66EF0");

    // Load and verify. Returns the reference ONLY when the on-disk content hashes (LF-normalized) to
    // the pinned sha; a tampered/superseded file returns null (illuminated), and an absent file
    // returns null (graceful — no library grounding, e.g. when it is not shipped in this build).
    public TotemReference? LoadVerified()
    {
        if (!File.Exists(_path)) return null;                         // absent → no grounding (graceful)
        byte[] raw = File.ReadAllBytes(_path);
        string sha = Sha256UpperLf(raw);
        if (!string.Equals(sha, _pinnedSha256, StringComparison.Ordinal)) return null; // tamper/version → illuminated
        string content = new UTF8Encoding(false).GetString(raw).TrimStart('﻿');
        return new TotemReference(_version, sha, content);
    }

    // A cheap integrity check without returning the content.
    public bool Verifies() => LoadVerified() is not null;

    static string Sha256UpperLf(byte[] raw) =>
        Convert.ToHexString(SHA256.HashData(NormalizeCrlf(raw)));

    // Drop the CR of any CRLF pair (CRLF -> LF), matching the pinned hash's normalization.
    static byte[] NormalizeCrlf(byte[] raw)
    {
        var outp = new List<byte>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == 0x0D && i + 1 < raw.Length && raw[i + 1] == 0x0A) continue;
            outp.Add(raw[i]);
        }
        return outp.ToArray();
    }
}
