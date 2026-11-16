using Microsoft.Toolkit.Uwp.Notifications;

namespace Lamha.App.Services;

/// <summary>Windows notifications ("toasts") for prayer reminders.</summary>
public static class ToastService
{
    public static void Show(string title, string body)
    {
        try
        {
            new ToastContentBuilder().AddText(title).AddText(body).Show();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex); // Notifications turned off in Windows: ignore.
        }
    }

    /// <summary>Removes Lamha's notifications and registration on exit.</summary>
    public static void Cleanup()
    {
        try { ToastNotificationManagerCompat.Uninstall(); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }
}
