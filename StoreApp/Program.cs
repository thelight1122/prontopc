using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ProntoPC;

class Program
{
    const string CloudRun = "https://pronto-pc-108655745293.us-central1.run.app";

    static void Main(string[] args)
    {
        // Diagnostic self-test: collect and summarize, no elevation, no POST.
        if (args.Length > 0 && args[0] == "--selftest")
        {
            var diag = Diagnostics.Collect();
            string full = JsonSerializer.Serialize(diag);
            Console.WriteLine("SECTIONS (" + diag.Count + "): " + string.Join(", ", diag.Keys));
            Console.WriteLine("TOTAL JSON BYTES: " + Encoding.UTF8.GetByteCount(full));
            foreach (var k in new[] { "os", "cpu", "memory", "uptime" })
                if (diag.ContainsKey(k)) Console.WriteLine(k + " => " + JsonSerializer.Serialize(diag[k]));
            foreach (var k in new[] { "disk", "browser_caches", "temp_files", "device_errors", "startup_programs", "smart_health", "bsod_history" })
                if (diag.TryGetValue(k, out var v) && v is System.Collections.IList list) Console.WriteLine(k + " count: " + list.Count);
            if (diag.TryGetValue("drivers", out var dv) && dv is Dictionary<string, object?> dd)
                Console.WriteLine("drivers.total=" + dd.GetValueOrDefault("total") + " signed=" + dd.GetValueOrDefault("signed"));
            if (diag.TryGetValue("installed_programs", out var ip) && ip is Dictionary<string, object?> ipd)
                Console.WriteLine("installed_programs.total_count=" + ipd.GetValueOrDefault("total_count"));
            return;
        }

        // URI activation: Windows passes the full prontopc:// URI as args[0]
        if (args.Length == 0)
        {
            ShowMessage("ProntoPC Launcher", "Open this app from the ProntoPC website after completing checkout.");
            return;
        }

        string uri = args[0];
        string token = ExtractToken(uri);

        if (string.IsNullOrEmpty(token))
        {
            ShowMessage("ProntoPC Error", "Invalid launch link. Please return to prontopc.online and try again.");
            return;
        }

        string mode = ExtractMode(uri);

        // Verification runs read-only after a reboot — no elevation needed.
        if (mode == "verify")
        {
            try { RunVerify(token).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.WriteLine("Verify error: " + ex.Message); }
            return;
        }

        // Self-elevate to Administrator if not already elevated
        if (!IsAdmin())
        {
            var elevate = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule!.FileName,
                Arguments = $"\"{uri}\"",
                Verb = "runas",
                UseShellExecute = true
            };
            try { Process.Start(elevate); }
            catch (Exception ex) when (ex.Message.Contains("cancel"))
            {
                // User clicked No on UAC — inform them
                ShowMessage("Administrator Required",
                    "ProntoPC needs Administrator access to scan your PC.\n\nPlease click Yes when Windows asks for permission.");
            }
            return;
        }

        // Elevated: run the requested step natively — no PowerShell, no downloaded
        // scripts, nothing executed from the network.
        try
        {
            if (mode == "repair")
            {
                Console.WriteLine("ProntoPC — Running Your Approved Repairs");
                Console.WriteLine();
                RunRepair(token).GetAwaiter().GetResult();
                Console.WriteLine();
                Console.WriteLine("Repairs complete. Return to your browser to see your report.");
            }
            else
            {
                Console.WriteLine("ProntoPC Diagnostic");
                Console.WriteLine("Reading your system — about 30-60 seconds. Nothing is changed.");
                Console.WriteLine();
                RunScan(token).GetAwaiter().GetResult();
                Console.WriteLine();
                Console.WriteLine("Scan complete. Return to your browser to review the findings.");
            }
        }
        catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }

        Console.WriteLine("Press any key to close.");
        Console.ReadKey();
    }

    // prontopc://scan?... or prontopc://repair?... — the host selects the step.
    static string ExtractMode(string uri)
    {
        try { return new Uri(uri).Host.ToLowerInvariant(); } catch { return "scan"; }
    }

    static async Task RunScan(string token)
    {
        Console.WriteLine("Collecting diagnostics...");
        var diag = Diagnostics.Collect();

        Console.WriteLine("Submitting results...");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        string payload = JsonSerializer.Serialize(new { token, diagnostics = diag });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await http.PostAsync($"{CloudRun}/api/submit-diagnostics", content);
        if (!resp.IsSuccessStatusCode)
            Console.WriteLine($"Server returned {(int)resp.StatusCode}. Please return to prontopc.online and try again.");
    }

    static async Task RunRepair(string token)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        Console.WriteLine("Fetching your approved repairs...");
        string body = await http.GetStringAsync($"{CloudRun}/api/session/{Uri.EscapeDataString(token)}/repair-actions");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("actions", out var actionsEl) || actionsEl.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("No approved repairs found. Please return to prontopc.online.");
            return;
        }

        var results = new List<Dictionary<string, object?>>();
        int total = actionsEl.GetArrayLength(), current = 0;
        foreach (var el in actionsEl.EnumerateArray())
        {
            current++;
            var action = JsonElementToDict(el);
            string name = action.GetValueOrDefault("friendly_name")?.ToString() ?? action.GetValueOrDefault("id")?.ToString() ?? "";
            Console.WriteLine($"  [{current}/{total}] {name}...");
            var r = Repair.Execute(action);
            results.Add(r);
            int mb = Convert.ToInt32(r["mb_freed"]);
            Console.WriteLine($"      {r["status"]}" + (mb > 0 ? $" — {mb} MB freed" : ""));
        }

        Console.WriteLine("Submitting results...");
        string payload = JsonSerializer.Serialize(new { token, results });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        await http.PostAsync($"{CloudRun}/api/submit-repair-results", content);

        if (Repair.NeedsRestart(results))
            PromptRestartAndReboot(token);
    }

    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string VerifyValueName = "ProntoPC_Verify";

    // Registers a one-time post-reboot verification, counts down (cancellable), then reboots.
    static void PromptRestartAndReboot(string token)
    {
        // Register a one-time verification that runs after the next sign-in.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            string launch = $"explorer.exe \"prontopc://verify?token={Uri.EscapeDataString(token)}\"";
            key.SetValue(VerifyValueName, launch);
        }
        catch { }

        Console.WriteLine();
        Console.WriteLine("A restart is recommended to finish applying these repairs.");
        Console.WriteLine("Your PC will restart in 60 seconds. Press any key to cancel and restart later.");
        bool cancelled = false;
        for (int sec = 60; sec >= 1; sec--)
        {
            if (sec % 15 == 0 || sec <= 5) Console.WriteLine($"  Restarting in {sec}...");
            for (int t = 0; t < 10; t++)
            {
                try { if (Console.KeyAvailable) { Console.ReadKey(true); cancelled = true; break; } } catch { }
                Thread.Sleep(100);
            }
            if (cancelled) break;
        }

        if (cancelled)
        {
            // Remove the pending verification so it never fires unexpectedly.
            try { using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true); key?.DeleteValue(VerifyValueName, throwOnMissingValue: false); } catch { }
            Console.WriteLine();
            Console.WriteLine("Restart cancelled. Please restart your PC manually when convenient to finish.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Restarting now — ProntoPC will verify the repairs automatically after reboot.");
            try { Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/r /t 8 /c \"ProntoPC is restarting to finish your repairs.\"", UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { Console.WriteLine("Could not restart automatically: " + ex.Message); }
        }
    }

    // Post-reboot: remove the startup entry, run read-only checks, report back.
    static async Task RunVerify(string token)
    {
        // Remove the one-time startup entry FIRST so this can never run twice.
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true); key?.DeleteValue(VerifyValueName, throwOnMissingValue: false); } catch { }

        Thread.Sleep(8000); // let services settle after login
        Console.WriteLine("ProntoPC — Verifying your repairs...");

        var verify = Diagnostics.Verify();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string payload = JsonSerializer.Serialize(new { token, verification = verify });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try { await http.PostAsync($"{CloudRun}/api/verify-results", content); } catch { }

        bool healthy = verify.TryGetValue("healthy", out var h) && h is bool b && b;
        Console.WriteLine();
        Console.WriteLine(healthy
            ? "All clear — your PC restarted cleanly and the repairs are holding."
            : "Restart complete. A few services are still starting up — usually normal right after boot.");
        Console.WriteLine("Return to your browser to see the updated report.");
        Thread.Sleep(6000);
    }

    static Dictionary<string, object?> JsonElementToDict(JsonElement el)
    {
        var d = new Dictionary<string, object?>();
        if (el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
                d[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? (object)l : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => p.Value.ToString(),
                };
        return d;
    }

    static string ExtractToken(string uri)
    {
        try
        {
            // prontopc://scan?token=xxxxxxxxxxxxxxxxxxxxxxxx
            var u = new Uri(uri);
            string query = u.Query.TrimStart('?');
            foreach (string part in query.Split('&'))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "token")
                    return Uri.UnescapeDataString(kv[1]);
            }
            return string.Empty;
        }
        catch { return string.Empty; }
    }

    static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    static void ShowMessage(string title, string message)
    {
        // Native message box — no PowerShell, no extra dependencies.
        MessageBox(IntPtr.Zero, message, title, 0x40); // MB_ICONINFORMATION
    }
}
