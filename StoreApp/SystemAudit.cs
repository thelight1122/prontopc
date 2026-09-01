using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// The PodEval harness compiles the real Assess() path (below) to prove that an
// arbitrary or incomplete audit can never be wrapped into a VALID SystemAudit.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PodEval")]

namespace ProntoPC;

// The immutable System Audit report — the ONLY thing the plan Umbilical will carry
// to the on-device model. The application (not the model) performs the audit; this
// record fixes what was collected (version + UTC completion time + completeness +
// an evidence hash) so a synthetic or half-finished diagnostic set cannot reach the
// model dressed up as a real one.
//
// Construction is deliberately closed: there is NO public constructor. A valid audit
// exists only if SystemAuditor.Assess built it from a completed Diagnostics run. The
// single public factory, Failed(), produces an explicitly INVALID audit ("the audit
// did not complete"), which the Orchestrator rejects before any author is consulted.
sealed class SystemAudit
{
    public string Version { get; }
    public DateTime CompletedAtUtc { get; }
    public JsonElement Diagnostics { get; }
    public int SectionsExpected { get; }
    public int SectionsPresent { get; }
    public int SectionsOk { get; }
    public string EvidenceHash { get; }
    public bool IsValid { get; }
    public string InvalidReason { get; }

    // internal (not public): only ProntoPC/PodEval code may build one, and in the
    // shipping assembly the only builder is SystemAuditor.Assess.
    internal SystemAudit(
        string version, DateTime completedAtUtc, JsonElement diagnostics,
        int sectionsExpected, int sectionsPresent, int sectionsOk,
        string evidenceHash, bool isValid, string invalidReason)
    {
        Version = version;
        CompletedAtUtc = completedAtUtc;
        Diagnostics = diagnostics;
        SectionsExpected = sectionsExpected;
        SectionsPresent = sectionsPresent;
        SectionsOk = sectionsOk;
        EvidenceHash = evidenceHash;
        IsValid = isValid;
        InvalidReason = invalidReason;
    }

    // "The audit did not complete." An invalid audit carrying no diagnostics and no
    // evidence hash — the Orchestrator declines it before reaching the model.
    public static SystemAudit Failed(string reason) =>
        new SystemAudit("1", DateTime.UtcNow, default, SystemAuditor.ExpectedCount, 0, 0, "", false, reason);
}

// Runs the deterministic audit (app code, never the model) and validates it into an
// immutable SystemAudit. Assess() is the sole gate that can mint a VALID audit.
static partial class SystemAuditor
{
    // The 25 sections Diagnostics.Collect() always attempts, in order. A COMPLETE
    // audit is one where all 25 were attempted (each is then PRESENT — a failed
    // section is still present but its value carries an "error" key).
    static readonly string[] Expected =
    {
        "os", "uptime", "cpu", "memory", "disk", "smart_health", "temp_files",
        "recycle_bin", "browser_caches", "windows_caches", "update_leftovers",
        "drivers", "device_errors", "bsod_history", "network", "dns_cache",
        "security", "activation", "startup_programs", "services",
        "installed_programs", "user_accounts", "power_plan", "visual_effects",
        "battery",
    };

    internal static int ExpectedCount => Expected.Length;

    // The ordered section keys a complete audit requires. Exposed (internal) so the
    // focused audit tests can assemble a complete/incomplete diagnostic set without
    // duplicating the list.
    internal static System.Collections.Generic.IReadOnlyList<string> ExpectedKeys => Expected;

    // Validate a completed diagnostic collection into an immutable audit. This is the
    // ONLY path that can produce IsValid == true.
    internal static SystemAudit Assess(Dictionary<string, object?> diagnostics)
    {
        var diagEl = JsonSerializer.SerializeToElement(diagnostics);

        int present = 0, ok = 0;
        foreach (var key in Expected)
        {
            if (!diagnostics.TryGetValue(key, out var val)) continue;
            present++;
            if (!HasError(val)) ok++;
        }

        // Evidence hash: SHA-256 (uppercase hex) over the canonical serialized
        // diagnostics — binds this audit's identity to exactly what was collected.
        string hash = Sha256Upper(JsonSerializer.Serialize(diagnostics));

        bool valid = present == Expected.Length;   // all 25 attempted == completed
        string invalidReason = valid ? "" : "incomplete_audit";

        return new SystemAudit(
            version: "1",
            completedAtUtc: DateTime.UtcNow,
            diagnostics: diagEl,
            sectionsExpected: Expected.Length,
            sectionsPresent: present,
            sectionsOk: ok,
            evidenceHash: hash,
            isValid: valid,
            invalidReason: invalidReason);
    }

    // A present section is "ok" unless its value is an object carrying an "error"
    // key (how Diagnostics.Section records a per-section failure).
    static bool HasError(object? val) =>
        val is IDictionary<string, object?> d && d.ContainsKey("error");

    static string Sha256Upper(string s)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
