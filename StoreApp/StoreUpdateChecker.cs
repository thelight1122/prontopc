using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace ProntoPC;

sealed class StoreUpdateChecker
{
    readonly StoreContext _context;

    public StoreUpdateChecker(IntPtr windowHandle)
    {
        _context = StoreContext.GetDefault();
        try { WinRT.Interop.InitializeWithWindow.Initialize(_context, windowHandle); }
        catch
        {
            // Some Store APIs work without an HWND. A failed bind should not block launch.
        }
    }

    public async Task<StoreUpdateCheckResult> CheckAsync()
    {
        string currentVersion = CurrentPackageVersion();
        try
        {
            IReadOnlyList<StorePackageUpdate> updates = await _context.GetAppAndOptionalStorePackageUpdatesAsync();
            if (updates.Count == 0)
                return StoreUpdateCheckResult.Current(currentVersion);

            var versions = new List<string>();
            foreach (var update in updates)
            {
                try
                {
                    if (update.Package is not null)
                        versions.Add(FormatVersion(update.Package.Id.Version));
                }
                catch
                {
                    // Some Store update entries do not expose a package identity to desktop callers.
                }
            }
            string versionText = string.Join(", ", versions.Distinct());

            return StoreUpdateCheckResult.Available(
                currentVersion,
                string.IsNullOrWhiteSpace(versionText) ? "A ProntoPC update is available." : $"ProntoPC update available: {versionText}");
        }
        catch (Exception ex)
        {
            return StoreUpdateCheckResult.Unavailable(currentVersion, ex.Message);
        }
    }

    public async Task<string> RequestInstallAsync()
    {
        IReadOnlyList<StorePackageUpdate> updates = await _context.GetAppAndOptionalStorePackageUpdatesAsync();
        if (updates.Count == 0)
            return "ProntoPC is already up to date.";

        StorePackageUpdateResult result = await _context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
        return result.OverallState switch
        {
            StorePackageUpdateState.Completed => "Update installed. Restart ProntoPC if Windows asks.",
            StorePackageUpdateState.Canceled => "Update canceled.",
            StorePackageUpdateState.ErrorLowBattery => "Update paused because the battery is low.",
            StorePackageUpdateState.ErrorWiFiRecommended => "Update paused until Wi-Fi is available.",
            StorePackageUpdateState.ErrorWiFiRequired => "Update needs Wi-Fi.",
            _ => "Update request sent to Microsoft Store."
        };
    }

    static string CurrentPackageVersion()
    {
        try { return FormatVersion(Package.Current.Id.Version); }
        catch { return "unpackaged"; }
    }

    static string FormatVersion(PackageVersion version) =>
        $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
}

readonly record struct StoreUpdateCheckResult(
    bool IsSupported,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string Message)
{
    public static StoreUpdateCheckResult Current(string currentVersion) =>
        new(true, false, currentVersion, "ProntoPC is up to date.");

    public static StoreUpdateCheckResult Available(string currentVersion, string message) =>
        new(true, true, currentVersion, message);

    public static StoreUpdateCheckResult Unavailable(string currentVersion, string reason) =>
        new(false, false, currentVersion, string.IsNullOrWhiteSpace(reason)
            ? "Update check is not available in this launch context."
            : $"Update check is not available in this launch context: {reason}");
}
