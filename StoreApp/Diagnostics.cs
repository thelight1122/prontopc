using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ProntoPC;

// Native diagnostic collector. Produces the exact `diag` JSON the Cloud Run
// backend expects — WITHOUT invoking PowerShell or downloading/executing any
// remote code. Read-only: it only queries system state, never modifies it.
//
// Each section is collected defensively (matching the PowerShell script's
// per-section try/catch): a failure records { error = "..." } instead of
// aborting the whole scan.
static class Diagnostics
{
    public static Dictionary<string, object?> Collect()
    {
        var d = new Dictionary<string, object?>();
        Section(d, "os", OsInfo);
        Section(d, "uptime", Uptime);
        Section(d, "cpu", Cpu);
        Section(d, "memory", Memory);
        Section(d, "disk", Disk);
        Section(d, "smart_health", SmartHealth);
        Section(d, "temp_files", TempFiles);
        Section(d, "recycle_bin", RecycleBin);
        Section(d, "browser_caches", BrowserCaches);
        Section(d, "windows_caches", WindowsCaches);
        Section(d, "drivers", Drivers);
        Section(d, "device_errors", DeviceErrors);
        Section(d, "bsod_history", BsodHistory);
        Section(d, "network", Network);
        Section(d, "dns_cache", DnsCache);
        Section(d, "security", Security);
        Section(d, "activation", Activation);
        Section(d, "startup_programs", StartupPrograms);
        Section(d, "services", Services);
        Section(d, "installed_programs", InstalledPrograms);
        Section(d, "user_accounts", UserAccounts);
        Section(d, "power_plan", PowerPlan);
        Section(d, "visual_effects", VisualEffects);
        Section(d, "battery", Battery);
        return d;
    }

    // Lightweight post-restart verification: temp size, critical services, health.
    public static Dictionary<string, object?> Verify()
    {
        var v = new Dictionary<string, object?>();
        try { var t = Environment.GetEnvironmentVariable("TEMP"); long b = (!string.IsNullOrEmpty(t) && Directory.Exists(t)) ? DirSize(t).bytes : 0; v["temp_mb"] = (int)Math.Round(b / 1048576.0); }
        catch { v["temp_mb"] = null; }

        var critical = new HashSet<string>(new[] { "wuauserv", "WinDefend", "mpssvc", "EventLog", "Dhcp", "Dnscache", "RpcSs" }, StringComparer.OrdinalIgnoreCase);
        var stopped = new List<object>();
        try
        {
            foreach (var o in Wmi("SELECT Name, State FROM Win32_Service"))
            {
                var n = S(o, "Name") ?? "";
                if (critical.Contains(n) && !(S(o, "State") ?? "").Equals("Running", StringComparison.OrdinalIgnoreCase)) stopped.Add(n);
            }
        }
        catch { }
        v["stopped_critical"] = stopped;
        v["new_critical_errors"] = null; // event-log check omitted (no extra deps); report renders null as "—"
        v["healthy"] = stopped.Count == 0;
        return v;
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    static void Section(Dictionary<string, object?> d, string key, Func<object?> fn)
    {
        try { d[key] = fn(); }
        catch (Exception ex) { d[key] = new Dictionary<string, object?> { ["error"] = ex.Message }; }
    }

    static IEnumerable<ManagementObject> Wmi(string query, string scope = @"root\cimv2")
    {
        using var s = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query));
        foreach (ManagementObject o in s.Get()) yield return o;
    }
    static ManagementObject? WmiFirst(string query, string scope = @"root\cimv2") => Wmi(query, scope).FirstOrDefault();

    static string? S(ManagementBaseObject o, string p) { try { return o[p]?.ToString(); } catch { return null; } }
    static long L(ManagementBaseObject o, string p) { try { return o[p] == null ? 0 : Convert.ToInt64(o[p]); } catch { return 0; } }
    static int I(ManagementBaseObject o, string p) { try { return o[p] == null ? 0 : Convert.ToInt32(o[p]); } catch { return 0; } }
    static bool? B(ManagementBaseObject o, string p) { try { return o[p] == null ? (bool?)null : Convert.ToBoolean(o[p]); } catch { return null; } }
    static DateTime? Dmtf(ManagementBaseObject o, string p) { try { var v = o[p]?.ToString(); return string.IsNullOrEmpty(v) ? (DateTime?)null : ManagementDateTimeConverter.ToDateTime(v); } catch { return null; } }
    static double Round(double v, int digits = 1) => Math.Round(v, digits);
    static string Iso(DateTime dt) => dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    static (long bytes, int count) DirSize(string path)
    {
        long bytes = 0; int count = 0;
        try
        {
            foreach (var f in SafeFiles(path)) { try { bytes += new FileInfo(f).Length; count++; } catch { } }
        }
        catch { }
        return (bytes, count);
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

    // ── sections ─────────────────────────────────────────────────────────────
    static object OsInfo()
    {
        var os = WmiFirst("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
        var cpu = WmiFirst("SELECT Name FROM Win32_Processor");
        return new Dictionary<string, object?>
        {
            ["version"] = Environment.OSVersion.Version.ToString(),
            ["release"] = Environment.OSVersion.Version.Major.ToString(),
            ["edition"] = os != null ? S(os, "Caption") : null,
            ["build"] = os != null ? S(os, "BuildNumber") : null,
            ["machine"] = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
            ["processor"] = cpu != null ? S(cpu, "Name") : null,
            ["hostname"] = Environment.MachineName,
        };
    }

    static object Uptime()
    {
        var os = WmiFirst("SELECT LastBootUpTime FROM Win32_OperatingSystem");
        var boot = os != null ? Dmtf(os, "LastBootUpTime") : null;
        if (boot == null) return new Dictionary<string, object?> { ["error"] = "Could not determine" };
        var up = DateTime.Now - boot.Value;
        return new Dictionary<string, object?>
        {
            ["boot_time"] = Iso(boot.Value),
            ["uptime_hours"] = Round(up.TotalHours),
            ["uptime_days"] = up.Days,
            ["needs_reboot"] = up.Days > 14,
        };
    }

    static object Cpu()
    {
        var p = WmiFirst("SELECT Name, NumberOfCores, ThreadCount, LoadPercentage, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor")
                ?? throw new Exception("No CPU");
        return new Dictionary<string, object?>
        {
            ["processor_name"] = S(p, "Name"),
            ["physical_cores"] = I(p, "NumberOfCores"),
            ["logical_cores"] = I(p, "ThreadCount"),
            ["current_percent"] = I(p, "LoadPercentage"),
            ["current_clock_mhz"] = I(p, "CurrentClockSpeed"),
            ["max_clock_mhz"] = I(p, "MaxClockSpeed"),
        };
    }

    static object Memory()
    {
        var m = WmiFirst("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem")
                ?? throw new Exception("No memory info");
        double totalGb = Round(L(m, "TotalVisibleMemorySize") / 1024.0 / 1024.0);
        double freeGb = Round(L(m, "FreePhysicalMemory") / 1024.0 / 1024.0);
        double usedGb = Round(totalGb - freeGb);
        double pct = totalGb > 0 ? Math.Round(usedGb / totalGb * 100, 0) : 0;
        return new Dictionary<string, object?>
        {
            ["total_gb"] = totalGb, ["available_gb"] = freeGb, ["used_gb"] = usedGb, ["percent_used"] = pct,
            ["health_status"] = pct > 90 ? "critical" : pct > 80 ? "warning" : "healthy",
        };
    }

    static object Disk()
    {
        var list = new List<object>();
        foreach (var o in Wmi("SELECT DeviceID, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk WHERE DriveType=3"))
        {
            long size = L(o, "Size"), free = L(o, "FreeSpace");
            if (size == 0) continue;
            double pct = Math.Round((double)(size - free) / size * 100, 0);
            list.Add(new Dictionary<string, object?>
            {
                ["device"] = S(o, "DeviceID"),
                ["total_gb"] = Round(size / 1073741824.0),
                ["used_gb"] = Round((size - free) / 1073741824.0),
                ["free_gb"] = Round(free / 1073741824.0),
                ["percent_used"] = pct,
                ["fstype"] = S(o, "FileSystem"),
                ["health_status"] = pct > 95 ? "critical" : pct > 85 ? "warning" : "healthy",
            });
        }
        return list;
    }

    static object SmartHealth()
    {
        var list = new List<object>();
        foreach (var o in Wmi("SELECT InstanceName, PredictFailure, Reason FROM MSStorageDriver_FailurePredictStatus", @"root\wmi"))
        {
            var name = S(o, "InstanceName") ?? "";
            list.Add(new Dictionary<string, object?>
            {
                ["drive"] = name.Substring(0, Math.Min(40, name.Length)),
                ["failure_predicted"] = B(o, "PredictFailure"),
                ["reason"] = I(o, "Reason"),
            });
        }
        return list;
    }

    static object TempFiles()
    {
        var paths = new[] { Environment.GetEnvironmentVariable("TEMP"), @"C:\Windows\Temp",
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp") }
                    .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var list = new List<object>();
        foreach (var p in paths)
        {
            var (bytes, count) = DirSize(p!);
            list.Add(new Dictionary<string, object?> { ["path"] = p, ["file_count"] = count, ["size_mb"] = Round(bytes / 1048576.0) });
        }
        return list;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO info);

    static object RecycleBin()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        int hr = SHQueryRecycleBin(null, ref info);
        if (hr != 0) return new Dictionary<string, object?> { ["item_count"] = 0, ["size_mb"] = 0 };
        return new Dictionary<string, object?> { ["item_count"] = info.i64NumItems, ["size_mb"] = Round(info.i64Size / 1048576.0) };
    }

    static object BrowserCaches()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var map = new (string name, string path)[]
        {
            ("Chrome", Path.Combine(local, @"Google\Chrome\User Data\Default\Cache")),
            ("Chrome (Code Cache)", Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache")),
            ("Edge", Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache")),
            ("Edge (Code Cache)", Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache")),
            ("Brave", Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cache")),
            ("Opera", Path.Combine(appdata, @"Opera Software\Opera Stable\Cache")),
            ("Firefox", Path.Combine(local, @"Mozilla\Firefox\Profiles")),
        };
        var list = new List<object>();
        foreach (var (name, path) in map)
        {
            if (!Directory.Exists(path)) continue;
            var (bytes, count) = DirSize(path);
            if (bytes > 0) list.Add(new Dictionary<string, object?> { ["browser"] = name, ["path"] = path, ["size_mb"] = Round(bytes / 1048576.0), ["file_count"] = count });
        }
        return list;
    }

    static object WindowsCaches()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var specs = new (string name, string dir, string pattern)[]
        {
            ("thumbnail_cache", Path.Combine(local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),
            ("icon_cache", local, "iconcache_*.db"),
            ("prefetch", @"C:\Windows\Prefetch", "*.pf"),
        };
        var result = new Dictionary<string, object?>();
        foreach (var (name, dir, pattern) in specs)
        {
            if (!Directory.Exists(dir)) continue;
            long bytes = 0; int count = 0;
            try { foreach (var f in Directory.GetFiles(dir, pattern)) { try { bytes += new FileInfo(f).Length; count++; } catch { } } } catch { }
            if (bytes > 0) result[name] = new Dictionary<string, object?> { ["size_mb"] = Round(bytes / 1048576.0), ["file_count"] = count };
        }
        return result;
    }

    static object Drivers()
    {
        var all = Wmi("SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, DriverProviderName, IsSigned FROM Win32_PnPSignedDriver").ToList();
        var unsigned = all.Where(o => B(o, "IsSigned") == false).Take(10)
            .Select(o => (object)new Dictionary<string, object?> { ["device"] = S(o, "DeviceName"), ["manufacturer"] = S(o, "Manufacturer"), ["version"] = S(o, "DriverVersion") }).ToList();
        var cutoff = DateTime.Now.AddYears(-2);
        var outdated = new List<object>();
        foreach (var o in all)
        {
            var date = Dmtf(o, "DriverDate");
            var provider = S(o, "DriverProviderName");
            if (date == null || date >= cutoff || provider == "Microsoft") continue;
            outdated.Add(new Dictionary<string, object?>
            {
                ["device"] = S(o, "DeviceName"), ["version"] = S(o, "DriverVersion"),
                ["date"] = date.Value.ToString("yyyy-MM-dd"), ["provider"] = provider,
                ["age_days"] = (int)(DateTime.Now - date.Value).TotalDays,
            });
            if (outdated.Count >= 15) break;
        }
        return new Dictionary<string, object?>
        {
            ["total"] = all.Count,
            ["signed"] = all.Count(o => B(o, "IsSigned") == true),
            ["unsigned"] = unsigned, ["outdated"] = outdated,
        };
    }

    static object DeviceErrors()
    {
        var list = new List<object>();
        foreach (var o in Wmi("SELECT Name, ConfigManagerErrorCode, Status, Manufacturer FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
        {
            int code = I(o, "ConfigManagerErrorCode");
            list.Add(new Dictionary<string, object?>
            {
                ["device"] = S(o, "Name"), ["error_code"] = code, ["status"] = S(o, "Status"), ["manufacturer"] = S(o, "Manufacturer"),
                ["error_description"] = code switch
                {
                    1 => "Not configured correctly", 3 => "Driver corrupted", 10 => "Cannot start", 22 => "Disabled",
                    28 => "Drivers not installed", 31 => "Not working properly", 43 => "Driver reported problem",
                    _ => $"Error code {code}",
                },
            });
        }
        return list;
    }

    static object BsodHistory()
    {
        var list = new List<object>();
        try
        {
            var dir = @"C:\Windows\Minidump";
            if (Directory.Exists(dir))
                foreach (var f in new DirectoryInfo(dir).GetFiles("*.dmp").OrderByDescending(f => f.LastWriteTime).Take(10))
                    list.Add(new Dictionary<string, object?> { ["file"] = f.Name, ["date"] = Iso(f.LastWriteTime), ["size_kb"] = Round(f.Length / 1024.0) });
        }
        catch { }
        return list;
    }

    static object Network()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => (object)new Dictionary<string, object?> { ["name"] = n.Name, ["description"] = n.Description, ["speed"] = n.Speed }).ToList();

        bool? reachable = null;
        try { using var ping = new Ping(); reachable = ping.Send("8.8.8.8", 2000).Status == IPStatus.Success; } catch { }

        var interfaces = new List<object>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
        {
            try
            {
                var ipp = n.GetIPProperties();
                var gw = ipp.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (gw == null) continue;
                var ipv4 = ipp.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                var dns = ipp.DnsAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.ToString());
                interfaces.Add(new Dictionary<string, object?>
                {
                    ["name"] = n.Name, ["ipv4"] = ipv4?.Address.ToString(), ["gateway"] = gw.Address.ToString(),
                    ["dns_servers"] = string.Join(",", dns),
                });
            }
            catch { }
        }
        return new Dictionary<string, object?>
        {
            ["adapters"] = adapters,
            ["connectivity"] = new Dictionary<string, object?> { ["internet_reachable"] = reachable, ["interfaces"] = interfaces },
        };
    }

    static object DnsCache()
    {
        try { return new Dictionary<string, object?> { ["entries"] = Wmi("SELECT Entry FROM MSFT_DNSClientCache", @"root\StandardCimv2").Count() }; }
        catch { return new Dictionary<string, object?> { ["entries"] = 0 }; }
    }

    static object Security()
    {
        var sec = new Dictionary<string, object?>();
        try
        {
            var mp = WmiFirst("SELECT * FROM MSFT_MpComputerStatus", @"root\Microsoft\Windows\Defender");
            if (mp != null)
            {
                sec["defender"] = new Dictionary<string, object?>
                {
                    ["real_time_protection"] = B(mp, "RealTimeProtectionEnabled"),
                    ["antivirus_enabled"] = B(mp, "AntivirusEnabled"),
                    ["antispyware_enabled"] = B(mp, "AntispywareEnabled"),
                    ["behavior_monitor"] = B(mp, "BehaviorMonitorEnabled"),
                    ["signature_last_updated"] = Dmtf(mp, "AntivirusSignatureLastUpdated") is DateTime s ? Iso(s) : null,
                    ["last_quick_scan"] = Dmtf(mp, "QuickScanEndTime") is DateTime q ? Iso(q) : null,
                    ["last_full_scan"] = Dmtf(mp, "FullScanEndTime") is DateTime fu ? Iso(fu) : null,
                };
            }
            else sec["defender"] = new Dictionary<string, object?> { ["error"] = "Could not query Defender" };
        }
        catch (Exception ex) { sec["defender"] = new Dictionary<string, object?> { ["error"] = ex.Message }; }

        try
        {
            var profiles = Wmi("SELECT Name, Enabled FROM MSFT_NetFirewallProfile", @"root\StandardCimv2")
                .Select(o => (object)new Dictionary<string, object?> { ["name"] = S(o, "Name"), ["enabled"] = B(o, "Enabled") }).ToList();
            sec["firewall"] = new Dictionary<string, object?> { ["profiles"] = profiles };
        }
        catch (Exception ex) { sec["firewall"] = new Dictionary<string, object?> { ["error"] = ex.Message }; }
        return sec;
    }

    static object Activation()
    {
        var lic = Wmi("SELECT Name, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct")
            .FirstOrDefault(o => !string.IsNullOrEmpty(S(o, "PartialProductKey")) && (S(o, "Name") ?? "").Contains("Windows"));
        if (lic == null) return new Dictionary<string, object?> { ["is_activated"] = null };
        int code = I(lic, "LicenseStatus");
        var map = new Dictionary<int, string> { [0] = "Unlicensed", [1] = "Licensed", [2] = "OOBGrace", [3] = "OOTGrace", [4] = "NonGenuine", [5] = "Notification" };
        return new Dictionary<string, object?>
        {
            ["product"] = S(lic, "Name"), ["status_code"] = code,
            ["status"] = map.TryGetValue(code, out var st) ? st : null, ["is_activated"] = code == 1,
        };
    }

    static object StartupPrograms()
    {
        var list = new List<object>();
        var hives = new (string hive, RegistryKey root)[] { ("HKCU", Registry.CurrentUser), ("HKLM", Registry.LocalMachine) };
        foreach (var (hive, root) in hives)
        {
            try
            {
                using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key == null) continue;
                foreach (var name in key.GetValueNames())
                    list.Add(new Dictionary<string, object?>
                    {
                        ["name"] = name, ["command"] = key.GetValue(name)?.ToString(),
                        ["hive"] = hive, ["key"] = $@"{hive}:\Software\Microsoft\Windows\CurrentVersion\Run",
                    });
            }
            catch { }
        }
        return list;
    }

    static object Services()
    {
        var critical = new HashSet<string>(new[] { "wuauserv", "WinDefend", "mpssvc", "EventLog", "Dhcp", "Dnscache", "RpcSs", "Schedule", "Winmgmt" }, StringComparer.OrdinalIgnoreCase);
        var stopped = new List<object>();
        foreach (var o in Wmi("SELECT Name, DisplayName, State, StartMode FROM Win32_Service"))
        {
            var name = S(o, "Name") ?? "";
            if (!critical.Contains(name)) continue;
            var state = S(o, "State") ?? "";
            if (!state.Equals("Running", StringComparison.OrdinalIgnoreCase))
                stopped.Add(new Dictionary<string, object?> { ["name"] = name, ["display_name"] = S(o, "DisplayName"), ["status"] = state, ["start_type"] = S(o, "StartMode") });
        }
        return new Dictionary<string, object?> { ["stopped_critical"] = stopped };
    }

    static object InstalledPrograms()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var progs = new List<object>();
        var roots = new (RegistryKey root, string path)[]
        {
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        };
        foreach (var (root, path) in roots)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key == null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var k = key.OpenSubKey(sub);
                        var name = k?.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrWhiteSpace(name) || !seen.Add(name.ToLowerInvariant())) continue;
                        progs.Add(new Dictionary<string, object?>
                        {
                            ["name"] = name, ["version"] = k?.GetValue("DisplayVersion")?.ToString(),
                            ["publisher"] = k?.GetValue("Publisher")?.ToString(),
                            ["install_date"] = k?.GetValue("InstallDate")?.ToString(),
                            ["size_kb"] = k?.GetValue("EstimatedSize"),
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }
        return new Dictionary<string, object?> { ["total_count"] = progs.Count, ["programs"] = progs };
    }

    static object UserAccounts()
    {
        var list = new List<object>();
        foreach (var o in Wmi("SELECT Name, Disabled, LocalAccount FROM Win32_UserAccount WHERE LocalAccount=True"))
            list.Add(new Dictionary<string, object?> { ["name"] = S(o, "Name"), ["enabled"] = !(B(o, "Disabled") ?? false) });
        return list;
    }

    static object PowerPlan()
    {
        try
        {
            var pp = Wmi("SELECT ElementName, IsActive FROM Win32_PowerPlan", @"root\cimv2\power").FirstOrDefault(o => B(o, "IsActive") == true);
            return new Dictionary<string, object?> { ["active_plan"] = pp != null ? S(pp, "ElementName") : "Unknown" };
        }
        catch { return new Dictionary<string, object?> { ["active_plan"] = "Unknown" }; }
    }

    static object VisualEffects()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            var code = key?.GetValue("VisualFXSetting");
            var map = new Dictionary<int, string> { [0] = "Let Windows choose", [1] = "Best appearance", [2] = "Best performance", [3] = "Custom" };
            int c = code == null ? -1 : Convert.ToInt32(code);
            return new Dictionary<string, object?> { ["setting"] = map.TryGetValue(c, out var s) ? s : "Default", ["code"] = code == null ? null : c };
        }
        catch { return new Dictionary<string, object?> { ["setting"] = "Default", ["code"] = null }; }
    }

    static object Battery()
    {
        var bat = WmiFirst("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
        if (bat == null) return new Dictionary<string, object?> { ["present"] = false };
        return new Dictionary<string, object?>
        {
            ["present"] = true,
            ["percent"] = I(bat, "EstimatedChargeRemaining"),
            ["plugged_in"] = I(bat, "BatteryStatus") == 2,
        };
    }
}
