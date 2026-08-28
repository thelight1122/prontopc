using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProntoPC;

// The Action Envelope, client side. Loads the bundled action-envelope.json (the
// single source of truth authored by the repair-engine Orb, embedded via a csproj
// Link) and enforces it. The Orchestrator HOLDS one instance of this and KEYS the
// repair-plan Umbilical to it: it compiles the per-machine admissible set from the
// preconditions x live diagnostics, and validates any authored plan before consent.
//
// Safety is structural, not model-trusted: a plan can only contain actions that are
// in the catalogue AND admissible on THIS machine; risk / requires_restart are taken
// from the envelope, never from the author; estimated_mb is clamped to measured space.
//
// See pod/orbs/repair-engine/action-envelope.json and pod/orchestrator/SPEC.md.
sealed class Envelope
{
    readonly JsonElement _root;
    readonly string _sha256Upper;
    readonly string _version;
    readonly int _maxActions;
    readonly List<(string umbrella, string[] members)> _subsumption;

    Envelope(string json)
    {
        _sha256Upper = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        _root = JsonDocument.Parse(json).RootElement;
        _version = _root.TryGetProperty("envelope_version", out var v) ? v.GetString() ?? "" : "";
        _maxActions = _root.GetProperty("plan_bounds").TryGetProperty("max_actions", out var m) ? m.GetInt32() : 12;
        _subsumption = new();
        if (_root.GetProperty("plan_bounds").TryGetProperty("subsumption", out var subs))
            foreach (var s in subs.EnumerateArray())
                _subsumption.Add((s.GetProperty("umbrella").GetString() ?? "",
                    s.GetProperty("members").EnumerateArray().Select(x => x.GetString() ?? "").ToArray()));
    }

    // ── loading ──────────────────────────────────────────────────────────────
    public static Envelope Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        string name = asm.GetManifestResourceNames().First(n => n.EndsWith("action-envelope.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new System.IO.StreamReader(s);
        return new Envelope(r.ReadToEnd());
    }

    // Construct directly from JSON (tests, and any non-embedded load).
    public static Envelope FromJson(string json) => new Envelope(json);

    // ── umbilical keying: version + hash ───────────────────────────────────────
    public string Version => _version;
    public string Sha256Upper => _sha256Upper;
    public bool Matches(string refShaUpper) => string.Equals(_sha256Upper, refShaUpper, StringComparison.OrdinalIgnoreCase);

    JsonElement ActionDef(string id) =>
        _root.GetProperty("actions").TryGetProperty(id, out var a) ? a : default;
    bool Known(string id) => _root.GetProperty("actions").TryGetProperty(id, out _);

    // ── per-machine admissible set (precondition x diagnostics) ────────────────
    public HashSet<string> AdmissibleSet(JsonElement diag)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in _root.GetProperty("actions").EnumerateObject())
            if (Holds(a.Value.GetProperty("precondition"), diag))
                set.Add(a.Name);
        return set;
    }

    // ── validate an authored plan; return sanitized plan + dropped log ─────────
    public (JsonObject plan, List<string> dropped) Validate(JsonElement authored, JsonElement diag)
    {
        var dropped = new List<string>();
        var admissible = AdmissibleSet(diag);

        var outPlan = JsonNode.Parse(authored.GetRawText())!.AsObject();
        var kept = new JsonArray();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (outPlan.TryGetPropertyValue("actions", out var actionsNode) && actionsNode is JsonArray actions)
        {
            foreach (var node in actions)
            {
                if (node is not JsonObject act) continue;
                string id = act["id"]?.GetValue<string>() ?? "";

                if (!Known(id)) { dropped.Add($"{id}: not in catalogue"); continue; }
                if (!admissible.Contains(id)) { dropped.Add($"{id}: precondition false on this machine"); continue; }

                var def = ActionDef(id);
                if (def.TryGetProperty("required_params", out var reqs))
                {
                    bool missing = reqs.EnumerateArray().Any(p =>
                    {
                        string key = p.GetString() ?? "";
                        return act[key] is null || string.IsNullOrWhiteSpace(act[key]?.ToString());
                    });
                    if (missing) { dropped.Add($"{id}: missing required_param"); continue; }
                }
                if (!seenIds.Add(id)) { dropped.Add($"{id}: duplicate"); continue; }

                // stamp risk + requires_restart from the envelope (never the author's)
                act["risk"] = def.GetProperty("risk").GetString();
                act["requires_restart"] = def.TryGetProperty("requires_restart", out var rr) && rr.GetBoolean();
                // clamp estimated_mb_recovered to the envelope mb bound
                act["estimated_mb_recovered"] = ClampMb(def, diag, act);

                kept.Add(act.DeepClone());
            }
        }

        // subsumption: drop members whose umbrella is present
        foreach (var (umbrella, members) in _subsumption)
            if (seenIds.Contains(umbrella))
                foreach (var mem in members)
                    if (seenIds.Remove(mem))
                    {
                        var idx = FindIndex(kept, mem);
                        if (idx >= 0) { kept.RemoveAt(idx); dropped.Add($"{mem}: subsumed by {umbrella}"); }
                    }

        // enforce max_actions
        while (kept.Count > _maxActions)
        {
            int last = kept.Count - 1;
            dropped.Add($"{(kept[last] as JsonObject)?["id"]?.GetValue<string>()}: over max_actions ({_maxActions})");
            kept.RemoveAt(last);
        }

        outPlan["actions"] = kept;
        return (outPlan, dropped);
    }

    static int FindIndex(JsonArray arr, string id)
    {
        for (int i = 0; i < arr.Count; i++)
            if ((arr[i] as JsonObject)?["id"]?.GetValue<string>() == id) return i;
        return -1;
    }

    int ClampMb(JsonElement def, JsonElement diag, JsonObject act)
    {
        int authored = act["estimated_mb_recovered"] is JsonValue v && v.TryGetValue<int>(out var iv) ? iv : 0;
        if (authored < 0) authored = 0;
        if (!def.TryGetProperty("mb", out var mb)) return authored;
        string source = mb.TryGetProperty("source", out var sc) ? sc.GetString() ?? "none" : "none";
        if (source == "zero" || source == "none") return 0;
        if (source == "field")
        {
            string field = mb.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "";
            bool gb = mb.TryGetProperty("unit", out var u) && u.GetString() == "gb";
            double cap = MeasureField(field, diag);
            if (gb) cap *= 1024.0;
            if (cap <= 0) return authored;              // no measure -> trust author (already >=0)
            return (int)Math.Min(authored, Math.Round(cap));
        }
        return authored;
    }

    // supports "a.b" scalar and "arr[].size_mb (sum)" array-sum forms used in the envelope
    static double MeasureField(string field, JsonElement diag)
    {
        int br = field.IndexOf("[]", StringComparison.Ordinal);
        if (br >= 0)
        {
            string arrPath = field[..br];
            string leaf = field[(br + 2)..].TrimStart('.').Split(' ')[0]; // ".size_mb (sum)" -> "size_mb"
            if (Resolve(arrPath, diag) is JsonElement arr && arr.ValueKind == JsonValueKind.Array)
                return arr.EnumerateArray().Sum(e => e.TryGetProperty(leaf, out var lv) && lv.TryGetDouble(out var d) ? d : 0);
            return 0;
        }
        return Resolve(field, diag) is JsonElement el && el.TryGetDouble(out var val) ? val : 0;
    }

    // ── predicate evaluator over the closed op set ─────────────────────────────
    bool Holds(JsonElement p, JsonElement diag)
    {
        string op = p.TryGetProperty("op", out var o) ? o.GetString() ?? "always" : "always";
        switch (op)
        {
            case "always": return true;
            case "and": return p.GetProperty("of").EnumerateArray().All(s => Holds(s, diag));
            case "or": return p.GetProperty("of").EnumerateArray().Any(s => Holds(s, diag));
        }

        var val = Resolve(p.GetProperty("field").GetString() ?? "", diag);
        switch (op)
        {
            case "present": return val is JsonElement pv && pv.ValueKind != JsonValueKind.Null && pv.ValueKind != JsonValueKind.Undefined;
            case "nonempty":
                if (val is not JsonElement nv) return false;
                return nv.ValueKind switch
                {
                    JsonValueKind.Array => nv.GetArrayLength() > 0,
                    JsonValueKind.Object => nv.EnumerateObject().Any(),
                    JsonValueKind.String => !string.IsNullOrEmpty(nv.GetString()),
                    _ => false
                };
            case "eq":
                if (val is not JsonElement ev) return false;
                return JsonEquals(ev, p.GetProperty("value"));
            case "gt":
            case "gte":
                if (val is JsonElement gv && gv.TryGetDouble(out var n) && p.GetProperty("value").TryGetDouble(out var t))
                    return op == "gt" ? n > t : n >= t;
                return false;
            default: return false;
        }
    }

    static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (b.ValueKind == JsonValueKind.True || b.ValueKind == JsonValueKind.False)
            return a.ValueKind == b.ValueKind;
        if (b.ValueKind == JsonValueKind.Number && a.TryGetDouble(out var an) && b.TryGetDouble(out var bn))
            return an == bn;
        if (b.ValueKind == JsonValueKind.String)
            return a.ValueKind == JsonValueKind.String && a.GetString() == b.GetString();
        return false;
    }

    // dotted-path resolver into a JsonElement diagnostics object; null if absent
    static JsonElement? Resolve(string path, JsonElement root)
    {
        JsonElement cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(seg, out var next)) return null;
            cur = next;
        }
        return cur;
    }
}
