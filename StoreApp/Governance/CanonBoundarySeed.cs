using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProntoPC.Governance;

// Canon Boundary: this module reads an exact external canonical source after verifying
// its declared hash. It does not carry an edited, app-owned Canon copy.
public static class CanonBoundarySeed
{
    public static readonly CanonReference ProntoPcCanonV1 = new(
        "AEGIS CORE FOUNDATIONAL CANON v1.0",
        "Canon-Locked 2026-05-31",
        @"I:\AEGIS-CORE-ENGINE\docs\AEGIS CORE FOUNDATIONAL CANON v1.0.md",
        "CF9F9C8E08E0AC2245C5D3DC0FDFFE1DC155F470E1A7BDD4D5B3543F76DDDC2F");

    public static CanonSeedResult LoadExact(CanonReference reference)
    {
        if (!File.Exists(reference.SourcePath))
            return new CanonSeedResult(CanonSeedAvailability.SourceUnavailable, reference, null,
                "The canonical source is not locally available. No Canon-seeded prompt was produced.");

        byte[] bytes = File.ReadAllBytes(reference.SourcePath);
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, reference.Sha256, StringComparison.OrdinalIgnoreCase))
            return new CanonSeedResult(CanonSeedAvailability.HashMismatch, reference, null,
                "The canonical source hash differs from the declared reference. No Canon-seeded prompt was produced.");

        string canon = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        string prompt = "CANON BOUNDARY\n"
            + "The external AEGIS Canon named below is the source authority. This application does not own, rewrite, or maintain a private doctrinal variant.\n"
            + $"Title: {reference.Title}\nVersion: {reference.Version}\nSource: {reference.SourcePath}\nSHA-256: {reference.Sha256}\n\n"
            + "BEGIN EXACT EXTERNAL CANON SOURCE\n"
            + canon
            + "\nEND EXACT EXTERNAL CANON SOURCE\n";
        return new CanonSeedResult(CanonSeedAvailability.Available, reference, prompt,
            "The exact hash-matching canonical source is available for the Custodian system prompt.");
    }
}
