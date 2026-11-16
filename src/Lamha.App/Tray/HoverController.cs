using System.Windows.Threading;
using Lamha.App.Interop;
using Lamha.App.Panel;

namespace Lamha.App.Tray;

/// <summary>
/// Opens the panel when the mouse rests on the tray icon for 250 ms and closes it
/// 400 ms after the mouse leaves both the icon and the panel. A click pins it open.
/// </summary>
public sealed class HoverController
{
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(400);
    private const int IconRadiusPx = 24; // How far from the icon still counts as "on the icon".

    private readonly PanelWindow _panel;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private Native.POINT _anchor;
    private DateTime? _hoverStarted;
    private DateTime _lastInside;

    public HoverController(PanelWindow panel)
    {
        _panel = panel;
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Last place the icon was seen. Used to open the panel from the hotkey.</summary>
    public Native.POINT? LastAnchor { get; private set; }

    /// <summary>Called on every mouse move over the tray icon.</summary>
    public void OnIconHover()
    {
        var cursor = Native.CursorPos();
        if (_panel.IsVisible)
        {
            _lastInside = DateTime.UtcNow;
            return;
        }
        if (_hoverStarted is null)
        {
            _anchor = cursor;
            _hoverStarted = DateTime.UtcNow;
            _timer.Start();
        }
    }

    /// <summary>Called on left click (or touch tap): toggles a pinned panel.</summary>
    public void OnIconClick()
    {
        _hoverStarted = null;
        if (_panel.JustClosedByOutsideClick) return; // This click already closed it.
        if (_panel.IsVisible && _panel.IsPinned)
        {
            _panel.HidePanel();
            return;
        }
        _anchor = Native.CursorPos();
        LastAnchor = _anchor;
        _panel.ShowNear(_anchor, pinned: true);
    }

    /// <summary>Hotkey: toggles a pinned panel at the icon's last known place (or bottom-right of the main screen).</summary>
    public void TogglePinned()
    {
        _hoverStarted = null;
        if (_panel.IsVisible)
        {
            _panel.HidePanel();
            return;
        }
        var anchor = LastAnchor ?? DefaultAnchor();
        _anchor = anchor;
        _panel.ShowNear(anchor, pinned: true);
    }

    private static Native.POINT DefaultAnchor()
    {
        // Just below the bottom-right corner of the main screen's work area = where the tray usually is.
        var primary = Native.MonitorFromPoint(new Native.POINT { X = 0, Y = 0 }, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(primary, ref info)) return new Native.POINT { X = 1880, Y = 1050 };
        return new Native.POINT { X = info.rcWork.Right - 40, Y = info.rcWork.Bottom + 10 };
    }

    private void Tick()
    {
        var cursor = Native.CursorPos();
        var now = DateTime.UtcNow;

        if (_hoverStarted is DateTime started)
        {
            if (!NearAnchor(cursor))
            {
                _hoverStarted = null; // Mouse passed by without stopping.
            }
            else if (now - started >= OpenDelay)
            {
                _hoverStarted = null;
                LastAnchor = _anchor;
                _lastInside = now;
                _panel.ShowNear(_anchor, pinned: false);
            }
        }
        else if (_panel.IsVisible && !_panel.IsPinned)
        {
            if (_panel.ContainsCursor(cursor) || NearAnchor(cursor)) _lastInside = now;
            else if (now - _lastInside >= CloseDelay) _panel.HidePanel();
        }

        if (_hoverStarted is null && (!_panel.IsVisible || _panel.IsPinned)) _timer.Stop();
    }

    private bool NearAnchor(Native.POINT p) =>
        Math.Abs(p.X - _anchor.X) <= IconRadiusPx && Math.Abs(p.Y - _anchor.Y) <= IconRadiusPx;
}
