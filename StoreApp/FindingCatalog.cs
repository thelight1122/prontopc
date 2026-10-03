using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ProntoPC;

// Deterministic, application-owned description of every Action Envelope action.
// The on-device model chooses WHICH admissible actions go in the plan; it never names
// them. Each findings row, the repair progress line, the report and the Technician's
// grounding all read the title / what-it-does / measured evidence from here, keyed by
// the action id, so the customer sees exactly what the read-only scan measured
// (inspection is deterministic — the model only explains supplied results).
static class FindingCatalog
{
    public sealed record Entry(string Title, string What);

    static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clear_temp_files"] = new("Clear temporary files", "Deletes leftover temporary files in your user Temp folder and C:\\Windows\\Temp. Programs recreate what they need."),
        ["clear_all_browser_caches"] = new("Clear browser caches", "Deletes cached web page files for your installed browsers. Passwords, bookmarks and history are not touched."),
        ["clear_chrome_cache"] = new("Clear Chrome cache", "Deletes Chrome's cached web page files. Passwords, bookmarks and history are not touched."),
        ["clear_edge_cache"] = new("Clear Edge cache", "Deletes Edge's cached web page files. Passwords, bookmarks and history are not touched."),
        ["clear_firefox_cache"] = new("Clear Firefox cache", "Deletes Firefox's cached web page files. Passwords, bookmarks and history are not touched."),
        ["clear_brave_cache"] = new("Clear Brave cache", "Deletes Brave's cached web page files. Passwords, bookmarks and history are not touched."),
        ["clear_opera_cache"] = new("Clear Opera cache", "Deletes Opera's cached web page files. Passwords, bookmarks and history are not touched."),
        ["clear_windows_caches"] = new("Refresh Windows caches", "Clears the thumbnail, icon and Prefetch caches. Windows rebuilds them as you use your PC."),
        ["empty_recycle_bin"] = new("Empty the Recycle Bin", "Permanently removes the files already in your Recycle Bin."),
        ["disk_cleanup"] = new("Clean safe temporary files", "Clears safe temporary files directly without opening the Windows Disk Cleanup window."),
        ["clear_windows_store_cache"] = new("Reset the Microsoft Store cache", "Runs Windows' wsreset tool, which fixes many Store download and update problems. The Microsoft Store opens when it finishes."),
        ["clean_windows_update_cache"] = new("Clean the Windows Update download cache", "Removes already-downloaded update files. Windows downloads again anything it still needs."),
        ["clean_component_store"] = new("Clean up old Windows components", "Runs Windows' DISM component cleanup to remove superseded system component versions."),
        ["clean_delivery_optimization"] = new("Clear Delivery Optimization files", "Removes the cache Windows uses to share updates between PCs."),
        ["remove_windows_old"] = new("Remove the previous Windows installation", "Deletes C:\\Windows.old, the copy of your previous Windows kept after an upgrade. You can no longer roll back to it afterwards."),
        ["flush_dns"] = new("Refresh the DNS cache", "Clears Windows' saved list of web addresses, which fixes many can't-reach-site problems."),
        ["optimize_power_plan"] = new("Set a balanced power plan", "Switches Windows to the Balanced power plan."),
        ["optimize_visual_effects"] = new("Reduce visual effects", "Sets Windows visual effects to Best Performance to ease memory and graphics load."),
        ["sfc_scan"] = new("Check and repair Windows system files", "Runs Windows' System File Checker (sfc /scannow), which finds and repairs damaged system files."),
        ["dism_restore"] = new("Repair the Windows system image", "Runs DISM RestoreHealth, which repairs the Windows image that system file repairs rely on."),
        ["chkdsk_scan"] = new("Check the C: drive for file-system errors", "Runs Windows' chkdsk in online scan mode, reporting file-system problems on C:."),
        ["reset_network_stack"] = new("Reset the network connection settings", "Resets Winsock, TCP/IP and DNS settings, which fixes many no-internet problems. A restart finishes it."),
        ["reset_windows_update"] = new("Reset Windows Update", "Restarts the Windows Update services and renames their caches so updates can start fresh."),
        ["disable_startup_item"] = new("Stop a program from starting with Windows", "Removes one program from the Windows startup list. The program itself stays installed."),
        ["driver_report"] = new("Driver report", "Records unsigned or outdated drivers in your report. Nothing is changed."),
        ["hardware_report"] = new("Hardware report", "Records hardware errors and recent crash data in your report. Nothing is changed."),
    };

    public static Entry For(string? id) =>
        id is not null && Entries.TryGetValue(id, out var e)
            ? e
            : new Entry(Humanize(id), "A repair from the ProntoPC repair list.");

    // One plain-language line of what the read-only scan actually measured for this
    // action, or "" when the action has no measured field (e.g. an always-offered tune-up).
    public static string Evidence(string? id, JsonElement diag, IReadOnlyDictionary<string, object?>? action = null)
    {
        try
        {
            switch (id)
            {
                case "clear_temp_files":
                    return SumList(diag, "temp_files", out int tmb, out int tfiles, out int dirs)
                        ? $"Measured: {Mb(tmb)} in {tfiles:N0} files across {dirs} temp folder{(dirs == 1 ? "" : "s")}." : "";
                case "clear_all_browser_caches": case "clear_chrome_cache": case "clear_edge_cache":
                case "clear_firefox_cache": case "clear_brave_cache": case "clear_opera_cache":
                    return BrowserEvidence(diag);
                case "empty_recycle_bin":
                    return Get(diag, "recycle_bin") is JsonElement rb
                        ? $"Measured: {Num(rb, "item_count"):N0} items, {Mb(Num(rb, "size_mb"))}." : "";
                case "clean_windows_update_cache":
                    return $"Measured: {Mb(Num(Get(diag, "update_leftovers"), "windows_update_cache_mb"))} of downloaded updates.";
                case "clean_delivery_optimization":
                    return $"Measured: {Mb(Num(Get(diag, "update_leftovers"), "delivery_optimization_mb"))} of Delivery Optimization cache.";
                case "remove_windows_old":
                    return Get(diag, "update_leftovers") is JsonElement ul && ul.TryGetProperty("windows_old_gb", out var gb) && gb.ValueKind == JsonValueKind.Number
                        ? $"Measured: C:\\Windows.old is {gb.GetDouble().ToString("0.0", CultureInfo.InvariantCulture)} GB." : "Measured: C:\\Windows.old is present.";
                case "flush_dns":
                    {
                        int entries = Num(Get(diag, "dns_cache"), "entries");
                        bool offline = Get(diag, "network") is JsonElement n && n.TryGetProperty("connectivity", out var c)
                            && c.TryGetProperty("internet_reachable", out var r) && r.ValueKind == JsonValueKind.False;
                        return offline ? $"Measured: the internet was not reachable during the scan; {entries:N0} saved DNS entries."
                                       : $"Measured: {entries:N0} saved DNS entries.";
                    }
                case "optimize_visual_effects":
                    return $"Measured: memory {Num(Get(diag, "memory"), "percent_used")}% in use.";
                case "sfc_scan": case "dism_restore": case "chkdsk_scan": case "hardware_report":
                    return HealthEvidence(diag);
                case "reset_network_stack":
                    return "Measured: the internet was not reachable during the scan.";
                case "reset_windows_update":
                    return $"Measured: {Mb(Num(Get(diag, "update_leftovers"), "windows_update_cache_mb"))} Windows Update cache (a sign updates are stuck).";
                case "disable_startup_item":
                    {
                        string target = action?.GetValueOrDefault("target_name")?.ToString() ?? "";
                        int count = Get(diag, "startup_programs") is JsonElement sp && sp.ValueKind == JsonValueKind.Array ? sp.GetArrayLength() : 0;
                        return target.Length > 0 ? $"Program: {target}. Measured: {count} programs start with Windows."
                                                 : $"Measured: {count} programs start with Windows.";
                    }
                case "driver_report":
                    {
                        var d = Get(diag, "drivers");
                        return $"Measured: {Len(d, "unsigned")} unsigned and {Len(d, "outdated")} outdated drivers.";
                    }
                default:
                    return "";
            }
        }
        catch { return ""; }
    }

    static string BrowserEvidence(JsonElement diag)
    {
        if (Get(diag, "browser_caches") is not JsonElement bc || bc.ValueKind != JsonValueKind.Array) return "";
        var parts = bc.EnumerateArray()
            .Select(b => (name: b.TryGetProperty("browser", out var n) ? n.GetString() ?? "" : "", mb: Num(b, "size_mb")))
            .Where(p => p.name.Length > 0)
            .GroupBy(p => p.name).Select(g => $"{g.Key} {Mb(g.Sum(x => x.mb))}");
        string joined = string.Join(", ", parts);
        return joined.Length > 0 ? $"Measured: {joined}." : "";
    }

    static string HealthEvidence(JsonElement diag)
    {
        int crashes = Len(diag, "bsod_history"), devices = Len(diag, "device_errors");
        var bits = new List<string>();
        if (crashes > 0) bits.Add($"{crashes} recent crash report{(crashes == 1 ? "" : "s")} (blue screens)");
        if (devices > 0) bits.Add($"{devices} device{(devices == 1 ? "" : "s")} reporting errors");
        return bits.Count > 0 ? "Measured: " + string.Join(" and ", bits) + "." : "";
    }

    static bool SumList(JsonElement diag, string field, out int mb, out int files, out int count)
    {
        mb = files = count = 0;
        if (Get(diag, field) is not JsonElement arr || arr.ValueKind != JsonValueKind.Array) return false;
        foreach (var e in arr.EnumerateArray()) { mb += Num(e, "size_mb"); files += Num(e, "file_count"); count++; }
        return count > 0;
    }

    static JsonElement? Get(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) ? v : null;

    static int Len(JsonElement? o, string name) =>
        o is JsonElement e && Get(e, name) is JsonElement a && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0;
    static int Len(JsonElement o, string name) => Len((JsonElement?)o, name);

    static int Num(JsonElement? o, string name) =>
        o is JsonElement e && Get(e, name) is JsonElement v && v.ValueKind == JsonValueKind.Number
            ? (int)Math.Round(v.GetDouble()) : 0;

    static string Mb(int mb) => mb >= 1024 ? $"{mb / 1024.0:0.0} GB" : $"{mb} MB";

    static string Humanize(string? id) =>
        string.IsNullOrWhiteSpace(id) ? "Repair"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));
}
