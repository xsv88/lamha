using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Lamha.App.Interop;
using Lamha.App.Tray;
using Lamha.Core.Settings;
using System.Windows.Controls;

namespace Lamha.App.Panel;

/// <summary>
/// The panel that appears above the tray icon. Borderless, always on top,
/// hidden from the taskbar and from Alt+Tab.
/// </summary>
public partial class PanelWindow : Window
{
    private IntPtr _hwnd;
    private Native.POINT _anchor;
    private double _opacity = 1.0;

    public PanelWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OnHandleCreated();
        Deactivated += (_, _) => { if (IsPinned) HidePanel(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) HidePanel(); };
        // A section can appear later (e.g. music starts): keep the panel anchored to the icon.
        SizeChanged += (_, _) => { if (IsVisible) PlaceAt(_anchor); };
    }

    /// <summary>True after a click or hotkey: stays open until click outside or Esc.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Raised when the panel opens or closes, so providers can start or stop sampling.</summary>
    public event Action<bool>? OpenChanged;

    /// <summary>Shows the panel next to a screen point (physical pixels) near the tray icon.</summary>
    public void ShowNear(Native.POINT anchor, bool pinned)
    {
        IsPinned = pinned;
        _anchor = anchor;
        bool wasVisible = IsVisible;

        if (!wasVisible)
        {
            Opacity = 0; // Avoid a flash at the old position while we measure.
            Show();
            OpenChanged?.Invoke(true); // Sections fill in now, so the size below is right.
        }

        PlaceAt(anchor);
        Opacity = _opacity;

        if (pinned) Activate();
    }

    /// <summary>Applies section order, hidden sections and opacity from settings.</summary>
    public void ApplySettings(AppSettings s)
    {
        _opacity = s.PanelOpacity;
        if (IsVisible) Opacity = _opacity;

        var hosts = new Dictionary<SectionId, FrameworkElement>
        {
            [SectionId.Header] = HeaderHost,
            [SectionId.Prayer] = PrayerHost,
            [SectionId.NowPlaying] = NowPlayingHost,
            [SectionId.PcLoad] = PcLoadHost,
        };
        Sections.Children.Clear();
        foreach (var id in s.SectionOrder)
        {
            if (!hosts.TryGetValue(id, out var host)) continue; // Phase 2/3 sections.
            host.Visibility = s.HiddenSections.Contains(id) ? Visibility.Collapsed : Visibility.Visible;
            Sections.Children.Add(host);
        }
    }

    public void HidePanel()
    {
        if (!IsVisible) return;
        IsPinned = false;
        Hide();
        OpenChanged?.Invoke(false);
    }

    /// <summary>True when the cursor (physical pixels) is over the panel.</summary>
    public bool ContainsCursor(Native.POINT p) =>
        IsVisible && Native.GetWindowRect(_hwnd, out var r)
        && p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;

    private void OnHandleCreated()
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Tool window: no taskbar button, not in Alt+Tab.
        var ex = (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, (IntPtr)(ex | Native.WS_EX_TOOLWINDOW));

        // Rounded corners on Windows 11 (ignored on Windows 10).
        int round = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private void PlaceAt(Native.POINT anchor)
    {
        IntPtr monitor = Native.MonitorFromPoint(anchor, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfo(monitor, ref info);
        double scale = Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1.0;

        UpdateLayout();
        var panelPx = new PxSize(ActualWidth * scale, ActualHeight * scale);
        var work = new PxRect(info.rcWork.Left, info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
        var icon = new PxRect(anchor.X, anchor.Y, 1, 1);

        var r = PanelPlacement.Place(icon, work, panelPx, margin: 12 * scale);
        Native.SetWindowPos(_hwnd, IntPtr.Zero, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }
}
