using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Lamha.App.Interop;
using Microsoft.Win32;
using ThemeMode = Lamha.Core.Settings.ThemeMode;

namespace Lamha.App.Ui;

/// <summary>Light/dark colors and the Windows 11 blurred background (acrylic).</summary>
public static class Theme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic, like Windows flyouts.

    /// <summary>Windows 11 22H2 (build 22621) and later support the backdrop API.</summary>
    public static bool SupportsBackdrop => Environment.OSVersion.Version.Build >= 22621;

    public static bool IsDark(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => WindowsUsesDark(),
    };

    public static void Apply(Window w, bool dark)
    {
        w.Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xF3, 0xF3, 0xF3) : Color.FromRgb(0x1A, 0x1A, 0x1A));

        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero)
        {
            w.Background = Solid(dark);
            return;
        }

        int on = dark ? 1 : 0;
        Native.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));

        if (SupportsBackdrop && HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
        {
            // Let DWM paint the blurred background behind transparent WPF content.
            target.BackgroundColor = Colors.Transparent;
            var margins = new Native.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            int backdrop = DWMSBT_TRANSIENTWINDOW;
            Native.DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            // Light tint on top keeps text readable on busy wallpapers.
            w.Background = new SolidColorBrush(dark ? Color.FromArgb(0x99, 0x20, 0x20, 0x20) : Color.FromArgb(0x99, 0xF3, 0xF3, 0xF3));
        }
        else
        {
            w.Background = Solid(dark);
        }
    }

    private static SolidColorBrush Solid(bool dark) =>
        new(dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3));

    private static bool WindowsUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return true;
        }
    }
}
