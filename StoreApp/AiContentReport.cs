using System;

namespace ProntoPC;

// This report receives only text the user sees and can redact. No diagnostic,
// conversation history, filesystem, log, or credential access belongs here.
public static class AiContentReport
{
    public const string SupportEmail = "support@prontopc.online";
    public const string ExplanationUrl = "https://prontopc.online/ai-aegis";
    public const string Subject = "ProntoPC AI content report";

    public static string Compose(string version, string response, string concern) =>
        $"{Subject}\nProntoPC version: {version}\n\nReported AI content (reviewed by user):\n{response.Trim()}\n\nConcern:\n{concern.Trim()}\n";

    public static string? EmailDraftUri(string body)
    {
        string uri = $"mailto:{SupportEmail}?subject={Uri.EscapeDataString(Subject)}&body={Uri.EscapeDataString(body)}";
        // Avoid mail clients silently dropping a long draft. Preserve the full
        // report through Copy/Save instead of truncating what the user reviewed.
        return uri.Length <= 1800 ? uri : null;
    }
}
