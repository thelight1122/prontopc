using System;
using System.IO;
using System.Text.Json;

namespace ProntoPC;

// The one responder selected for the Product POD.  The source formation adapter is
// merged into this CPU/INT4 ONNX export; analyzer models remain development-only.
static class ProductModel
{
    public const string Name = "gemma-3-1b-it-formed-v0.2.1";

    public static string DirectoryPath => Path.Combine(
        AppContext.BaseDirectory, "Assets", "models", Name);

    // A model folder is usable only when its formation provenance travels with it.
    // The large weight hashes are validated in the release/package check rather than
    // recalculated during normal customer startup.
    public static bool IsPresentAndBound()
    {
        try
        {
            string config = Path.Combine(DirectoryPath, "genai_config.json");
            string provenance = Path.Combine(DirectoryPath, "aegis-formation-manifest.json");
            if (!File.Exists(config) || !File.Exists(provenance)) return false;
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(provenance));
            return doc.RootElement.GetProperty("model_name").GetString() == Name
                && doc.RootElement.GetProperty("canon_sha256").GetString()
                   == "CF9F9C8E08E0AC2245C5D3DC0FDFFE1DC155F470E1A7BDD4D5B3543F76DDDC2F";
        }
        catch { return false; }
    }
}
