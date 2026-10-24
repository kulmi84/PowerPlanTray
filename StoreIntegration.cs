using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;

namespace PowerPlanTray;

internal static class StoreIntegration
{
    internal const string StartupTaskId = "PowerPlanTrayStartup";
    public static bool IsPackaged { get; } = HasPackageIdentity();

    private static bool HasPackageIdentity()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, null) == 122; // ERROR_INSUFFICIENT_BUFFER
    }

    public static async Task<bool> IsStartupEnabledAsync()
    {
        var task = await StartupTask.GetAsync(StartupTaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    // A user's or administrator's Windows startup setting always takes precedence.
    public static async Task<string?> SetStartupEnabledAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(StartupTaskId);
        if (!enabled)
        {
            if (task.State == StartupTaskState.EnabledByPolicy)
                return L.T("Der Autostart wird durch eine Windows-Richtlinie verwaltet.");
            task.Disable();
            return null;
        }
        var state = await task.RequestEnableAsync();
        return state switch
        {
            StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => null,
            StartupTaskState.DisabledByUser => L.T("Der Autostart wurde in Windows deaktiviert. Bitte PowerPlanTray unter Einstellungen > Apps > Autostart wieder aktivieren."),
            StartupTaskState.DisabledByPolicy => L.T("Der Autostart wird durch eine Windows-Richtlinie verhindert."),
            _ => L.T("Der Autostart konnte nicht aktiviert werden. Bitte die Windows-Autostart-Einstellungen prüfen.")
        };
    }

    public static string SettingsDirectory => Windows.Storage.ApplicationData.Current.LocalFolder.Path;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
