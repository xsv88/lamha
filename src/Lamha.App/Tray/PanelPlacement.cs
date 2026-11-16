namespace Lamha.App.Tray;

// No WPF types in this file: it is also compiled into Lamha.Core.Tests.

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PxRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

public readonly record struct PxSize(double Width, double Height);

/// <summary>Decides where the panel appears so it sits next to the tray icon and stays on its monitor.</summary>
public static class PanelPlacement
{
    /// <param name="iconRect">Tray icon rectangle (Shell_NotifyIconGetRect).</param>
    /// <param name="workArea">Work area of the monitor that holds the icon (screen minus taskbar).</param>
    public static PxRect Place(PxRect iconRect, PxRect workArea, PxSize panel, double margin = 12)
    {
        double x, y;

        if (iconRect.Y >= workArea.Bottom)            // Taskbar at the bottom.
        {
            x = iconRect.CenterX - panel.Width / 2;
            y = workArea.Bottom - panel.Height - margin;
        }
        else if (iconRect.Bottom <= workArea.Y)       // Taskbar at the top.
        {
            x = iconRect.CenterX - panel.Width / 2;
            y = workArea.Y + margin;
        }
        else if (iconRect.Right <= workArea.X)        // Taskbar on the left.
        {
            x = workArea.X + margin;
            y = iconRect.CenterY - panel.Height / 2;
        }
        else if (iconRect.X >= workArea.Right)        // Taskbar on the right.
        {
            x = workArea.Right - panel.Width - margin;
            y = iconRect.CenterY - panel.Height / 2;
        }
        else                                          // Icon inside the work area (overflow menu): open above it.
        {
            x = iconRect.CenterX - panel.Width / 2;
            y = iconRect.Y - panel.Height - margin;
        }

        // Keep the panel fully inside the work area. If it is too tall, pin it to the top.
        x = Math.Max(workArea.X + margin, Math.Min(x, workArea.Right - panel.Width - margin));
        y = Math.Max(workArea.Y + margin, Math.Min(y, workArea.Bottom - panel.Height - margin));

        return new PxRect(x, y, panel.Width, panel.Height);
    }
}
