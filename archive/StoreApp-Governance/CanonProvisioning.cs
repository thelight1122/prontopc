using System;
using System.IO;
using System.Text.Json;

namespace ProntoPC.Governance;

// Installed-app Canon provenance. This resolver neither packages nor copies the Canon.
// It remains closed unless a caller supplies a manifest authorizer and the referenced
// canonical artifact passes CanonBoundarySeed's byte-level hash verification.
public sealed record CanonProvisioningManifest(
    string SchemaVersion,
    CanonReference Canon,
    string Provisioner,
    DateTimeOffset ProvisionedUtc,
    string AuthorizationEvidence);

public sealed record CanonProvisioningResult(bool IsAvailable, CanonReference? Reference, string Detail);

public static class CanonProvisioning
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string DefaultManifestPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProntoPC", "Governance", "canon-provisioning.json");

    public static string DefaultArtifactPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProntoPC", "Canon", "AEGIS CORE FOUNDATIONAL CANON v1.0.md");

    public static CanonProvisioningResult LoadAuthorized(string manifestPath, Func<CanonProvisioningManifest, bool>? isAuthorized)
    {
        if (isAuthorized is null)
            return new CanonProvisioningResult(false, null, "No manifest authorizer was supplied; the provisioned Canon remains unavailable.");
        if (!File.Exists(manifestPath))
            return new CanonProvisioningResult(false, null, "No Canon provisioning manifest is available.");
        try
        {
            CanonProvisioningManifest? manifest = JsonSerializer.Deserialize<CanonProvisioningManifest>(File.ReadAllText(manifestPath), Json);
            if (manifest?.Canon is null || string.IsNullOrWhiteSpace(manifest.AuthorizationEvidence) || !isAuthorized(manifest))
                return new CanonProvisioningResult(false, null, "The Canon provisioning manifest is not authorized.");
            CanonSeedResult seed = CanonBoundarySeed.LoadExact(manifest.Canon);
            return seed.Availability == CanonSeedAvailability.Available
                ? new CanonProvisioningResult(true, manifest.Canon, "The authorized provisioned Canon is available and hash-verified.")
                : new CanonProvisioningResult(false, null, seed.Detail);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CanonProvisioningResult(false, null, "The Canon provisioning manifest could not be read.");
        }
    }
}
