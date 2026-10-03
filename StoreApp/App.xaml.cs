using System;
using System.Diagnostics;
using System.Windows;
using Windows.ApplicationModel;

namespace ProntoPC;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            LogStartupFailure(e.Exception);
            e.Handled = true;
            MessageBox.Show(
                "ProntoPC could not finish starting. Please close it and open it again, or contact support@prontopc.online.",
                "ProntoPC",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) LogStartupFailure(ex);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string repairRequestPath = ExtractOption(GetActivationArguments(e.Args), "repair-request");

        // Certification launch must open a visible window without requiring admin.
        // Elevation is requested later, only after the customer explicitly chooses
        // to perform repairs that need it.
        new MainWindow(repairRequestPath).Show();
    }

    static string[] GetActivationArguments(string[] startupArgs)
    {
        try { _ = AppInstance.GetActivatedEventArgs(); }
        catch
        {
            // Unpackaged/debug invocation has no package lifecycle activation.
        }

        return startupArgs;
    }

    static string ExtractOption(string[] args, string name)
    {
        string prefix = "--" + name + "=";
        foreach (var a in args)
            if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(a.Substring(prefix.Length).Trim('"'));
        return "";
    }

    static void LogStartupFailure(Exception ex)
    {
        try
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProntoPC", "logs");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "startup-crash.log");
            System.IO.File.AppendAllText(path,
                DateTime.UtcNow.ToString("O") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            // Last-ditch crash logging must never become the crash.
        }
    }
}
