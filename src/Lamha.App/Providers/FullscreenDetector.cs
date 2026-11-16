using System.Runtime.InteropServices;

namespace Lamha.App.Providers;

/// <summary>Asks Windows whether a full-screen game, video or presentation is running.</summary>
public static partial class FullscreenDetector
{
    private enum QueryUserNotificationState
    {
        NotPresent = 1, Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4,
        AcceptsNotifications = 5, QuietTime = 6, App = 7,
    }

    public static bool IsBusy()
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) != 0) return false;
            return state is QueryUserNotificationState.Busy
                or QueryUserNotificationState.RunningD3DFullScreen
                or QueryUserNotificationState.PresentationMode;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return false;
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out QueryUserNotificationState state);
}
