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

    // Plain-English labels for the measured-space cleanups, in customer-facing display
    // order. Used to (a) name any action the model left unlabeled or echoed as a raw id,
    // and (b) label completeness-backstop additions (see Validate). These are the six
    // catalogue actions whose reclaimable space is MEASURED from the read-only scan
    // (mb.source == "field"); every other action's space is zero/none.
    static readonly (string id, string name, string category)[] SpaceActions =
    {
        ("clear_temp_files",            "Clear temporary files",                 "Cleanup"),
        ("clear_all_browser_caches",    "Clear browser caches",                  "Cleanup"),
        ("empty_recycle_bin",           "Empty the Recycle Bin",                 "Cleanup"),
        ("clean_windows_update_cache",  "Clear Windows Update cache",            "Cleanup"),
        ("clean_delivery_optimization", "Clear Windows Update delivery files",   "Cleanup"),
        ("remove_windows_old",          "Remove previous Windows installation",  "Cleanup"),
    };
    static (string name, string category)? Label(string id)
    {
        foreach (var a in SpaceActions) if (a.id == id) return (a.name, a.category);
        return null;
    }

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

                // stamp risk + requires_restart + kind from the envelope (never the author's)
                act["risk"] = def.GetProperty("risk").GetString();
                act["requires_restart"] = def.TryGetProperty("requires_restart", out var rr) && rr.GetBoolean();
                act["kind"] = def.TryGetProperty("kind", out var kd) ? kd.GetString() : "repair";
                // clamp estimated_mb_recovered to the envelope mb bound
                act["estimated_mb_recovered"] = ClampMb(def, diag, act);

                kept.Add(act.DeepClone());
            }
        }

        // ── completeness backstop ──────────────────────────────────────────────
        // The model authors the plan, but it sometimes OMITS a cleanup whose space the
        // read-only scan actually MEASURED (> 0 MB). Those are grounded, admissible facts
        // the customer paid to have found, so we surface every measured reclaim the model
        // left out. This is the same class of post-processing as ClampMb — the envelope,
        // not the model, owns the measured MB (TOTEM-0011); here it also owns whether a
        // measured cleanup appears at all. Stays inside I2: it runs only because a provider
        // actually authored a plan, and adds ONLY catalogue actions that are admissible on
        // THIS machine with a positive measured reclaim. The subsumption + max_actions
        // passes below apply to these additions too, so an added umbrella (browser caches)
        // is never double-counted with a per-browser member the model may have authored.
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, _, _) in SpaceActions)
        {
            if (seenIds.Contains(id)) continue;              // model already included it
            if (!admissible.Contains(id)) continue;          // precondition false on this machine
            var def = ActionDef(id);
            var candidate = new JsonObject { ["id"] = id };
            int mb = ClampMb(def, diag, candidate);          // envelope's measured reclaim
            if (mb <= 0) continue;                            // nothing measured to reclaim
            var lbl = Label(id)!.Value;
            candidate["category"] = lbl.category;
            candidate["friendly_name"] = lbl.name;
            candidate["risk"] = def.GetProperty("risk").GetString();
            candidate["requires_restart"] = def.TryGetProperty("requires_restart", out var rr) && rr.GetBoolean();
            candidate["kind"] = def.TryGetProperty("kind", out var kd) ? kd.GetString() : "repair";
            candidate["estimated_mb_recovered"] = mb;
            kept.Add(candidate);
            seenIds.Add(id);
            addedIds.Add(id);
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

        // Record which completeness additions actually survived (subsumption / max_actions
        // may have dropped one, logged in `dropped`) as auditable metadata on the local plan.
        // The approved action list is the single consent surface used by the repair runner.
        if (addedIds.Count > 0)
        {
            var survivors = new JsonArray();
            foreach (var node in kept)
                if (node is JsonObject ko && ko["id"]?.GetValue<string>() is string kid && addedIds.Contains(kid))
                    survivors.Add(kid);
            if (survivors.Count > 0) outPlan["completeness_added"] = survivors;
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
            // The envelope knows the true reclaimable amount from diagnostics, so it SETS
            // the estimate rather than trusting the model's guess — which both caps
            // over-claims and corrects under-claims. A truer sign than the model's number.
            return cap > 0 ? (int)Math.Round(cap) : 0;
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
