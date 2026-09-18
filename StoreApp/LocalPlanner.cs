using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

// The local-model Satellite (pod/orbs/repair-plan/satellites/local-model/).
// Authors the repair plan ON-DEVICE with an embedded small model, so the AI plan is
// present offline with no billable provider. It is the desktop's only plan author:
// a local decline remains local and is surfaced as an honest diagnostic outcome.
//
// The actual inference is isolated behind ILocalInference so the ONNX Runtime GenAI
// dependency and the ~1GB Gemma weights can be added when they land (Stage 2b) without
// touching the prompt/grammar/parse logic here.

interface ILocalInference
{
    // True only when a usable on-device model is loaded.
    bool Available { get; }
    string ModelName { get; }
    // Run the model. `jsonSchema` is passed through to model runtimes that support
    // guided decoding. Returns the completion text, or null on any failure.
    string? Complete(string prompt, string jsonSchema, CancellationToken ct);

    // Multi-turn free-form chat (the conversational Custodian — SPEC Phase 2). `history`
    // ends on the latest user turn; render it through the model's chat template and
    // generate the next model reply. No guided decoding — this is conversation, not the
    // schema-constrained plan authoring above. Returns the reply text, or null on failure.
    string? Chat(IReadOnlyList<ChatTurn> history, CancellationToken ct);
}

// Default until Stage 2b: no model bundled -> the Satellite declines every request.
sealed class AbsentInference : ILocalInference
{
    public bool Available => false;
    public string ModelName => "none";
    public string? Complete(string prompt, string jsonSchema, CancellationToken ct) => null;
    public string? Chat(IReadOnlyList<ChatTurn> history, CancellationToken ct) => null;
}

sealed class GemmaLocalPlanner : IPlanAuthor, IPlanAuthorDiagnostics
{
    readonly ILocalInference _model;
    public string LastDeclineReason { get; private set; } = "local_not_started";
    public GemmaLocalPlanner(ILocalInference model) { _model = model; }

    public async Task<PlanResult?> AuthorAsync(SystemAudit audit, IReadOnlySet<string> admissible, CancellationToken ct)
    {
        LastDeclineReason = "local_not_started";
        // System Audit precondition (TOTEM-X-0074): the model is never invoked on an
        // invalid/incomplete/absent audit. This is the model-facing enforcement point;
        // the Orchestrator gates the same invariant before the author is even reached.
        if (!audit.IsValid) { LastDeclineReason = "system_audit_invalid"; return null; }
        if (!_model.Available) { LastDeclineReason = "local_model_unavailable"; return null; }
        if (admissible.Count == 0) { LastDeclineReason = "no_admissible_actions"; return null; }

        // Only the validated audit's evidence reaches the prompt.
        string prompt = BuildPrompt(audit.Diagnostics, admissible);
        string schema = BuildSchema(admissible);

        string? text;
        try { text = await Task.Run(() => _model.Complete(prompt, schema, ct), ct); }
        catch (OperationCanceledException) { LastDeclineReason = "local_inference_cancelled"; return null; }
        catch { LastDeclineReason = "local_inference_failed"; return null; }
        if (string.IsNullOrWhiteSpace(text)) { LastDeclineReason = "local_completion_empty"; return null; }

        if (!TryExtractJson(text, out var plan)) { LastDeclineReason = "local_plan_json_invalid"; return null; }
        // The Orchestrator re-validates against the envelope (catalogue, admissibility,
        // mb clamp, risk stamp), so a slightly off plan is still made safe downstream.
        LastDeclineReason = "";
        return new PlanResult(plan, "local-model", _model.ModelName);
    }

    // Prompt uses the same compact diagnostic wording across local model builds, then
    // constrains the model to choose only from the admissible action ids.
    static string BuildPrompt(JsonElement diag, IReadOnlySet<string> admissible)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are ProntoPC's repair planner. From the diagnostics below, choose the");
        sb.AppendLine("repairs that will most help this PC. Choose ONLY from the allowed action ids.");
        sb.AppendLine("Reply with JSON: {\"summary\": string, \"actions\": [{\"id\": string, \"category\": string, \"friendly_name\": string, \"estimated_mb_recovered\": integer}]}.");
        sb.AppendLine("The summary must reference the machine's real numbers.");
        sb.AppendLine();
        sb.Append(NatureLayerContext.ForPlan());
        sb.AppendLine();
        sb.AppendLine("Allowed action ids: " + string.Join(", ", admissible.OrderBy(x => x)));
        sb.AppendLine();
        sb.AppendLine("Diagnostics:");
        sb.Append(DiagnosticText(diag));
        return sb.ToString();
    }

    // Compact diagnostic text used by local plan authoring.
    static string DiagnosticText(JsonElement d)
    {
        var lines = new List<string>();
        void Add(string s) => lines.Add(s);

        if (Get(d, "os") is JsonElement os && os.ValueKind == JsonValueKind.Object)
            Add($"OS: {Str(os, "edition")} build {Str(os, "build")} on {Str(os, "hostname")}");
        if (Get(d, "memory") is JsonElement m && m.ValueKind == JsonValueKind.Object)
            Add($"Memory: {Num(m, "used_gb")}/{Num(m, "total_gb")} GB ({Num(m, "percent_used")}%)");
        if (Get(d, "disk") is JsonElement disk && disk.ValueKind == JsonValueKind.Array)
            foreach (var dd in disk.EnumerateArray())
                Add($"Drive {Str(dd, "device")}: {Num(dd, "percent_used")}% used ({Num(dd, "free_gb")} GB free of {Num(dd, "total_gb")} GB)");
        if (Get(d, "temp_files") is JsonElement tf && tf.ValueKind == JsonValueKind.Array)
            Add($"Temp files: {tf.EnumerateArray().Sum(e => NumOf(e, "size_mb"))} MB");
        if (Get(d, "recycle_bin") is JsonElement rb && rb.ValueKind == JsonValueKind.Object)
            Add($"Recycle Bin: {Num(rb, "size_mb")} MB");
        if (Get(d, "browser_caches") is JsonElement bc && bc.ValueKind == JsonValueKind.Array && bc.GetArrayLength() > 0)
            Add($"Browser caches: {bc.EnumerateArray().Sum(e => NumOf(e, "size_mb"))} MB across {bc.GetArrayLength()} browser(s)");
        if (Get(d, "update_leftovers") is JsonElement ul && ul.ValueKind == JsonValueKind.Object)
        {
            var parts = new List<string>();
            if (NumOf(ul, "windows_update_cache_mb") > 0) parts.Add($"update cache {Num(ul, "windows_update_cache_mb")} MB");
            if (NumOf(ul, "delivery_optimization_mb") > 0) parts.Add($"Delivery Optimization {Num(ul, "delivery_optimization_mb")} MB");
            if (Get(ul, "windows_old_present") is JsonElement wo && wo.ValueKind == JsonValueKind.True)
                parts.Add($"Windows.old present ({Num(ul, "windows_old_gb")} GB)");
            if (parts.Count > 0) Add("Windows Update leftovers: " + string.Join(", ", parts));
        }
        if (Get(d, "startup_programs") is JsonElement sp && sp.ValueKind == JsonValueKind.Array && sp.GetArrayLength() > 0)
            Add($"Startup programs: {sp.GetArrayLength()}");
        if (Get(d, "bsod_history") is JsonElement bs && bs.ValueKind == JsonValueKind.Array && bs.GetArrayLength() > 0)
            Add($"Crash history: {bs.GetArrayLength()} minidump(s)");
        if (Get(d, "device_errors") is JsonElement de && de.ValueKind == JsonValueKind.Array && de.GetArrayLength() > 0)
            Add($"Device Manager problems: {de.GetArrayLength()}");
        if (Get(d, "network") is JsonElement net && Get(net, "connectivity") is JsonElement con
            && Get(con, "internet_reachable") is JsonElement ir && ir.ValueKind == JsonValueKind.False)
            Add("Internet connectivity: UNREACHABLE");
        return string.Join("\n", lines);
    }

    // A JSON schema restricting action.id to the admissible enum. The ILocalInference
    // impl (Stage 2b) translates this to the runtime's guided-decoding grammar.
    static string BuildSchema(IReadOnlySet<string> admissible)
    {
        string ids = string.Join(",", admissible.OrderBy(x => x).Select(x => JsonSerializer.Serialize(x)));
        return "{\"type\":\"object\",\"properties\":{\"summary\":{\"type\":\"string\"},"
             + "\"actions\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{"
             + "\"id\":{\"enum\":[" + ids + "]},\"category\":{\"type\":\"string\"},"
             + "\"friendly_name\":{\"type\":\"string\"},\"estimated_mb_recovered\":{\"type\":\"integer\"}},"
             + "\"required\":[\"id\",\"friendly_name\"]}}},\"required\":[\"summary\",\"actions\"]}";
    }

    // Models sometimes wrap JSON in prose; take the outermost {...}.
    static bool TryExtractJson(string text, out JsonElement plan)
    {
        plan = default;
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        try { plan = JsonDocument.Parse(text.Substring(start, end - start + 1)).RootElement.Clone(); return true; }
        catch { return false; }
    }

    // ── small JSON helpers ─────────────────────────────────────────────────────
    static JsonElement? Get(JsonElement o, string k) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(k, out var v) ? v : (JsonElement?)null;
    static string Str(JsonElement o, string k) => Get(o, k) is JsonElement v && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static double NumOf(JsonElement o, string k) => Get(o, k) is JsonElement v && v.TryGetDouble(out var d) ? d : 0;
    static string Num(JsonElement o, string k) { double v = NumOf(o, k); return v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.#"); }
}
