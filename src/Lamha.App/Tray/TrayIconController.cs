using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace Lamha.App.Tray;

/// <summary>Color of the tray icon. Blue = prayer soon, Yellow = waiting for iqama, Red = last minute.</summary>
public enum TrayAccent { None, Blue, Yellow, Red }

/// <summary>
/// Owns the icon next to the clock and its right-click menu.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private TrayAccent _accent = TrayAccent.None;

    public TrayIconController(Action onOpenPanel, Action onExit)
    {
        var settingsItem = new MenuItem { Header = "Settings", IsEnabled = false }; // Enabled in Task 9.
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        _icon = new TaskbarIcon
        {
            ToolTipText = "Lamha",
            IconSource = LoadIcon(TrayAccent.None),
            ContextMenu = new ContextMenu { Items = { settingsItem, new Separator(), exitItem } },
        };
        _icon.TrayLeftMouseUp += (_, _) => onOpenPanel();
        _icon.ForceCreate();
    }

    public void SetAccent(TrayAccent accent)
    {
        if (accent == _accent) return;
        _accent = accent;
        _icon.IconSource = LoadIcon(accent);
    }

    public void Dispose() => _icon.Dispose();

    private static BitmapImage LoadIcon(TrayAccent accent)
    {
        string name = accent switch
        {
            TrayAccent.Blue => "lamha-blue",
            TrayAccent.Yellow => "lamha-yellow",
            TrayAccent.Red => "lamha-red",
            _ => "lamha",
        };
        return new BitmapImage(new Uri($"pack://application:,,,/Assets/{name}.ico"));
    }
}
