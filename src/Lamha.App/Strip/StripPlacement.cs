using Lamha.App.Tray;

namespace Lamha.App.Strip;

// No WPF types in this file: it is also compiled into Lamha.Core.Tests.

/// <summary>Where the strip sits on a horizontal taskbar.</summary>
public static class StripPlacement
{
    private const double FallbackOffset = 260; // Typical width of the tray area at 100%.
    private const double GapToTray = 8;

    /// <param name="taskbar">Taskbar rectangle (physical pixels).</param>
    /// <param name="trayNotify">The notification area next to the clock, if found.</param>
    /// <param name="offsetFromRight">Saved drag position: distance from the taskbar's right edge to the strip's right edge.</param>
    /// <returns>Null when the taskbar is vertical: the strip is not shown there.</returns>
    public static PxRect? Place(PxRect taskbar, PxRect? trayNotify, PxSize strip, double? offsetFromRight, double scale)
    {
        if (taskbar.Height > taskbar.Width) return null;

        double right = offsetFromRight is double o ? taskbar.Right - o
            : trayNotify is PxRect t ? t.X - GapToTray * scale
            : taskbar.Right - FallbackOffset * scale;

        double x = Math.Clamp(right - strip.Width, taskbar.X, Math.Max(taskbar.X, taskbar.Right - strip.Width));
        double y = taskbar.Y + (taskbar.Height - strip.Height) / 2;
        return new PxRect(x, y, strip.Width, strip.Height);
    }
}
