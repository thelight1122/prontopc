using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;

namespace ProntoPC;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string token = ExtractToken(e.Args);

        // Self-elevate ONCE so scan + repair run with admin rights (single UAC).
        if (!IsAdmin())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName,
                    Arguments = string.Join(" ", e.Args.Select(a => "\"" + a + "\"")),
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { /* user declined UAC — nothing to do */ }
            Shutdown();
            return;
        }

        new MainWindow(token).Show();
    }

    static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Accepts prontopc://run?token=XXXX, --token=XXXX, or a bare 64-hex token.
    static string ExtractToken(string[] args)
    {
        foreach (var a in args)
        {
            int i = a.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                var t = a.Substring(i + 6);
                int amp = t.IndexOf('&');
                if (amp >= 0) t = t.Substring(0, amp);
                return Uri.UnescapeDataString(t.Trim('"', '/'));
            }
            if (Regex.IsMatch(a, "^[a-f0-9]{64}$")) return a;
        }
        return "";
    }
}
