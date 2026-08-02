using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

        // Elevated: run the read-only diagnostic natively — no PowerShell, no
        // downloaded scripts, nothing executed from the network. We only READ
        // system state, then POST the results to the server for the repair plan.
        Console.WriteLine("ProntoPC Diagnostic");
        Console.WriteLine("Reading your system — this takes about 30-60 seconds. Nothing is changed.");
        Console.WriteLine();

        try { RunScan(token).GetAwaiter().GetResult(); }
        catch (Exception ex) { Console.WriteLine("Scan error: " + ex.Message); }

        Console.WriteLine();
        Console.WriteLine("Scan complete. Return to your browser to review the findings.");
        Console.WriteLine("Press any key to close.");
        Console.ReadKey();
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
