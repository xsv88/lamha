using System.Runtime.InteropServices;
using Lamha.App.Interop;
using Lamha.App.Tray;

namespace Lamha.App.Strip;

/// <summary>Reads where the main Windows taskbar is, using documented window classes and AppBar messages.</summary>
public sealed record TaskbarInfo(PxRect Bounds, PxRect? TrayNotify, bool AutoHide, double Scale)
{
    public static TaskbarInfo? Read()
    {
        IntPtr bar = Native.FindWindow("Shell_TrayWnd", null);
        if (bar == IntPtr.Zero || !Native.GetWindowRect(bar, out var r)) return null; // Explorer restarting.

        PxRect? tray = null;
        IntPtr notify = Native.FindWindowEx(bar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify != IntPtr.Zero && Native.GetWindowRect(notify, out var t) && t.Right - t.Left > 0 && t.Bottom - t.Top > 0)
            tray = new PxRect(t.Left, t.Top, t.Right - t.Left, t.Bottom - t.Top);

        var data = new Native.APPBARDATA { cbSize = Marshal.SizeOf<Native.APPBARDATA>(), hWnd = bar };
        bool autoHide = ((long)Native.SHAppBarMessage(Native.ABM_GETSTATE, ref data) & Native.ABS_AUTOHIDE) != 0;

        IntPtr monitor = Native.MonitorFromWindow(bar, Native.MONITOR_DEFAULTTONEAREST);
        double scale = Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1.0;

        return new TaskbarInfo(new PxRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top), tray, autoHide, scale);
    }
}
