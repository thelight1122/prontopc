using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace ProntoPC;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Desktop Bridge does not reliably put a windows.protocol URI into WPF's
        // StartupEventArgs.  For a packaged launch, read the activation payload
        // from the package lifecycle API first, then retain command-line support
        // for unpackaged/debug launches and the elevated child process.
        string token = ExtractToken(GetActivationArguments(e.Args));

        // Self-elevate ONCE so scan + repair run with admin rights (single UAC).
        if (!IsAdmin())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName,
                    // The WPF command line can be empty for a packaged protocol
                    // launch.  Carry the parsed token across elevation explicitly.
                    // MainWindow still verifies it remotely before allowing a scan.
                    Arguments = string.IsNullOrWhiteSpace(token) ? "" : "--token=" + token,
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

    static string[] GetActivationArguments(string[] startupArgs)
    {
        try
        {
            var activated = AppInstance.GetActivatedEventArgs();
            if (activated?.Kind == ActivationKind.Protocol && activated is ProtocolActivatedEventArgs protocol)
                return new[] { protocol.Uri.OriginalString };
        }
        catch
        {
            // Unpackaged/debug invocation has no package lifecycle activation.
        }

        return startupArgs;
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
