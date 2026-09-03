using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ProntoPC;

// Product Nature is a bounded evidence-domain map, not an identity or a source of
// facts.  It is packaged with the Product and gives the formed responder stable
// names for organizing the validated observations it receives.
static class NatureLayerContext
{
    static readonly Lazy<string> Summary = new(LoadSummary);

    public static string ForPlan() => Summary.Value;

    static string LoadSummary()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "aegis",
                "repair-nature-layer.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            var sb = new StringBuilder();
            sb.AppendLine("Product Nature Layer: organize only the supplied diagnostic evidence within these bounded domains:");
            foreach (JsonElement aspect in doc.RootElement.GetProperty("aspects").EnumerateArray())
                sb.Append("- ").Append(aspect.GetProperty("name").GetString()).Append(": ")
                  .AppendLine(aspect.GetProperty("definition").GetString());
            sb.AppendLine("The diagnostic evidence remains the sole basis for a repair plan; do not infer missing facts.");
            return sb.ToString();
        }
        catch
        {
            // The plan remains fail-safe under the SystemAudit and Action Envelope rails.
            // Packaging verification catches a missing layer before release.
            return "Product Nature Layer unavailable; use only the supplied diagnostic evidence.";
        }
    }
}
