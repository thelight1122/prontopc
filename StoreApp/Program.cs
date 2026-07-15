using System;
using System.Diagnostics;
using System.Security.Principal;

class Program
{
    const string CloudRun = "https://pronto-pc-108655745293.us-central1.run.app";

    static void Main(string[] args)
    {
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

        // Elevated: run the diagnostic via irm | iex (no file downloaded, runs in memory)
        string scanUrl = $"{CloudRun}/scan?token={Uri.EscapeDataString(token)}";
        string psCmd = $"irm '{scanUrl}' | iex";

        Console.WriteLine("ProntoPC Diagnostic Starting...");
        Console.WriteLine("Please wait — this takes about 30 seconds.");
        Console.WriteLine();

        var ps = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -ExecutionPolicy Bypass -Command \"{psCmd}\"",
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        var proc = Process.Start(ps);
        proc?.WaitForExit();

        Console.WriteLine();
        Console.WriteLine("Scan complete. Press any key to close.");
        Console.ReadKey();
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

    static void ShowMessage(string title, string message)
    {
        // Use PowerShell to show a message box — no WinForms dependency needed
        var ps = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('{message}', '{title}')\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(ps)?.WaitForExit();
    }
}
