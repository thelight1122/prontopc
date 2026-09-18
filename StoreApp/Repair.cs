using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ProntoPC;

// Native repair executor. Runs the client-approved repair actions using .NET
// APIs (file/registry work) and by spawning the ordinary Windows system tools
// (sfc, DISM, chkdsk, netsh, cleanmgr, wsreset, powercfg) — NOT PowerShell, and
// nothing downloaded or executed from the network. Each action returns a result
// row matching what the local report writer expects:
//   { action, name, status: done|noted|skipped|error, mb_freed, detail }
//
// Actions that require a reboot to fully apply are reported via NeedsRestart.
static class Repair
{
    static readonly HashSet<string> RestartActions = new(StringComparer.OrdinalIgnoreCase)
    { "sfc_scan", "dism_restore", "reset_network_stack", "disable_startup_item", "reset_windows_update", "optimize_visual_effects" };

    public static Dictionary<string, object?> Execute(Dictionary<string, object?> action)
    {
        string id = action.TryGetValue("id", out var i) ? i?.ToString() ?? "" : "";
        string name = action.TryGetValue("friendly_name", out var n) ? n?.ToString() ?? id : id;
        var result = new Dictionary<string, object?> { ["action"] = id, ["name"] = name, ["status"] = "skipped", ["mb_freed"] = 0, ["detail"] = "" };
        try
        {
            switch (id)
            {
                case "clear_temp_files":
                    Freed(result, DeleteRecursive(new[] { Environment.GetEnvironmentVariable("TEMP"), @"C:\Windows\Temp" })); break;
                case "clear_chrome_cache": case "clear_edge_cache": case "clear_firefox_cache":
                case "clear_brave_cache": case "clear_opera_cache":
                    Freed(result, DeleteRecursive(BrowserPaths(id))); break;
                case "clear_all_browser_caches":
                    Freed(result, DeleteRecursive(AllBrowserPaths())); break;
                case "clear_windows_caches":
                    Freed(result, ClearWindowsCaches()); break;
                case "empty_recycle_bin":
                    SHEmptyRecycleBin(IntPtr.Zero, null, 0x7); Done(result, "Recycle Bin emptied"); break;
                case "flush_dns":
                    RunExe("ipconfig", "/flushdns"); Done(result, "DNS cache flushed"); break;
                case "sfc_scan":
                    Done(result, RunExe("sfc", "/scannow", 900)); break;
                case "dism_restore":
                    Done(result, RunExe("DISM", "/Online /Cleanup-Image /RestoreHealth", 900)); break;
                case "disk_cleanup":
                    RunExe("cleanmgr", "/sagerun:1", 600); Done(result, "Windows Disk Cleanup completed"); break;
                case "chkdsk_scan":
                    Done(result, RunExe("chkdsk", "C: /scan", 900)); break;
                case "reset_network_stack":
                    RunExe("netsh", "winsock reset"); RunExe("netsh", "int ip reset");
                    RunExe("ipconfig", "/release"); RunExe("ipconfig", "/renew"); RunExe("ipconfig", "/flushdns");
                    Done(result, "Network stack reset (winsock, IP, DNS)"); break;
                case "disable_startup_item":
                    DisableStartupItem(action, result); break;
                case "optimize_power_plan":
                    RunExe("powercfg", "/setactive SCHEME_BALANCED"); Done(result, "Power plan set to Balanced"); break;
                case "optimize_visual_effects":
                    SetVisualEffects(); Done(result, "Visual effects set to Best Performance"); break;
                case "reset_windows_update":
                    ResetWindowsUpdate(); Done(result, "Windows Update components reset"); break;
                case "clear_windows_store_cache":
                    RunExe("wsreset.exe", "", 120); Done(result, "Windows Store cache cleared"); break;
                case "clean_windows_update_cache":
                    Freed(result, CleanWindowsUpdateCache()); break;
                case "clean_component_store":
                    Done(result, RunExe("DISM", "/Online /Cleanup-Image /StartComponentCleanup", 900)); break;
                case "remove_windows_old":
                    RemoveWindowsOld(result); break;
                case "clean_delivery_optimization":
                    Freed(result, DeleteAllIn(@"C:\Windows\SoftwareDistribution\DeliveryOptimization")); break;
                case "driver_report": case "hardware_report":
                    result["status"] = "noted"; result["detail"] = "Documented in report. Physical service may be recommended."; break;
                default:
                    result["status"] = "noted"; result["detail"] = "Action logged for review"; break;
            }
        }
        catch (Exception ex) { result["status"] = "error"; result["detail"] = ex.Message; }
        return result;
    }

    public static bool NeedsRestart(IEnumerable<Dictionary<string, object?>> results) =>
        results.Any(r => (r.GetValueOrDefault("status")?.ToString() == "done") && RestartActions.Contains(r.GetValueOrDefault("action")?.ToString() ?? ""));

    // ── result helpers ───────────────────────────────────────────────────────
    static void Done(Dictionary<string, object?> r, string detail) { r["status"] = "done"; r["detail"] = detail ?? ""; }
    static void Freed(Dictionary<string, object?> r, long bytes) { r["status"] = "done"; r["mb_freed"] = (int)Math.Round(bytes / 1048576.0); }

    // ── file helpers ─────────────────────────────────────────────────────────
    static long DeleteRecursive(IEnumerable<string?> roots)
    {
        long freed = 0;
        foreach (var root in roots.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var f in SafeFiles(root!))
            { try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch { } }
        return freed;
    }
    static IEnumerable<string> SafeFiles(string root)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] subs = Array.Empty<string>(), files = Array.Empty<string>();
            try { subs = Directory.GetDirectories(dir); } catch { }
            try { files = Directory.GetFiles(dir); } catch { }
            foreach (var f in files) yield return f;
            foreach (var s in subs) stack.Push(s);
        }
    }

    static string[] BrowserPaths(string id)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return id switch
        {
            "clear_chrome_cache" => new[] { Path.Combine(local, @"Google\Chrome\User Data\Default\Cache"), Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache") },
            "clear_edge_cache" => new[] { Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache"), Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache") },
            "clear_firefox_cache" => new[] { Path.Combine(local, @"Mozilla\Firefox\Profiles") },
            "clear_brave_cache" => new[] { Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cache") },
            "clear_opera_cache" => new[] { Path.Combine(appdata, @"Opera Software\Opera Stable\Cache") },
            _ => Array.Empty<string>(),
        };
    }
    static string[] AllBrowserPaths()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new[]
        {
            Path.Combine(local, @"Google\Chrome\User Data\Default\Cache"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache"),
            Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
            Path.Combine(appdata, @"Opera Software\Opera Stable\Cache"),
        };
    }
    static long ClearWindowsCaches()
    {
        long freed = 0;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var globs = new (string dir, string pattern)[]
        {
            (Path.Combine(local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),
            (local, "iconcache_*.db"),
            (@"C:\Windows\Prefetch", "*.pf"),
        };
        foreach (var (dir, pattern) in globs)
        {
            if (!Directory.Exists(dir)) continue;
            string[] files = Array.Empty<string>();
            try { files = Directory.GetFiles(dir, pattern); } catch { }
            foreach (var f in files) { try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch { } }
        }
        return freed;
    }

    // ── registry actions ─────────────────────────────────────────────────────
    static void DisableStartupItem(Dictionary<string, object?> action, Dictionary<string, object?> result)
    {
        string name = action.GetValueOrDefault("target_name")?.ToString() ?? "";
        string hive = action.GetValueOrDefault("target_hive")?.ToString() ?? "HKCU";
        if (string.IsNullOrEmpty(name)) { result["status"] = "error"; result["detail"] = "No startup item specified"; return; }
        var root = hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        try
        {
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue(name) != null) { key.DeleteValue(name); Done(result, "Disabled: " + name); }
            else { result["status"] = "skipped"; result["detail"] = "Startup item not found: " + name; }
        }
        catch (Exception ex) { result["status"] = "error"; result["detail"] = "Could not disable: " + ex.Message; }
    }

    static void SetVisualEffects()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
        key.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
    }

    static void ResetWindowsUpdate()
    {
        var svcs = new[] { "wuauserv", "cryptSvc", "bits", "msiserver" };
        foreach (var s in svcs) RunExe("net", "stop " + s, 60);
        foreach (var (path, bak) in new[] { (@"C:\Windows\SoftwareDistribution", @"C:\Windows\SoftwareDistribution.bak"),
                                            (@"C:\Windows\System32\catroot2", @"C:\Windows\System32\catroot2.bak") })
        {
            try { if (Directory.Exists(path)) { if (Directory.Exists(bak)) Directory.Delete(bak, true); Directory.Move(path, bak); } } catch { }
        }
        foreach (var s in svcs) RunExe("net", "start " + s, 60);
    }

    // ── Windows Update leftovers ─────────────────────────────────────────────
    static long DirBytes(string path)
    {
        long b = 0;
        if (Directory.Exists(path)) foreach (var f in SafeFiles(path)) { try { b += new FileInfo(f).Length; } catch { } }
        return b;
    }
    static long DeleteAllIn(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        long freed = 0;
        foreach (var f in SafeFiles(dir)) { try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch { } }
        return freed;
    }
    static long CleanWindowsUpdateCache()
    {
        const string sd = @"C:\Windows\SoftwareDistribution\Download";
        long freed = DirBytes(sd);
        foreach (var s in new[] { "wuauserv", "bits" }) RunExe("net", "stop " + s, 60);
        if (Directory.Exists(sd))
        {
            foreach (var f in SafeFiles(sd)) { try { File.Delete(f); } catch { } }
            foreach (var d in Directory.GetDirectories(sd)) { try { Directory.Delete(d, true); } catch { } }
        }
        foreach (var s in new[] { "wuauserv", "bits" }) RunExe("net", "start " + s, 60);
        return freed;
    }
    static void RemoveWindowsOld(Dictionary<string, object?> result)
    {
        const string wo = @"C:\Windows.old";
        if (!Directory.Exists(wo)) { result["status"] = "skipped"; result["detail"] = "No Windows.old present"; return; }
        long freed = DirBytes(wo);
        RunExe("takeown", $"/f \"{wo}\" /r /d y", 600);
        RunExe("icacls", $"\"{wo}\" /grant *S-1-5-32-544:F /t /c", 600);
        try { Directory.Delete(wo, true); } catch { }
        if (!Directory.Exists(wo)) { result["status"] = "done"; result["mb_freed"] = (int)Math.Round(freed / 1048576.0); result["detail"] = "Previous Windows installation removed"; }
        else { result["status"] = "error"; result["detail"] = "Windows.old could not be fully removed"; }
    }

    // ── process helper (system tools only — never powershell) ─────────────────
    static string RunExe(string file, string args, int timeoutSec = 120)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = file, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p == null) return "";
            string outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutSec * 1000);
            outp = outp.Trim();
            return outp.Length > 300 ? outp.Substring(outp.Length - 300) : outp;
        }
        catch (Exception ex) { return ex.Message; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
