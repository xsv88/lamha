using System.Windows;
using Lamha.App.Panel;
using Lamha.App.Tray;

namespace Lamha.App;

/// <summary>
/// Entry point. Lamha has no main window: it lives as a tray icon.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayIconController? _tray;
    private PanelWindow? _panel;
    private HoverController? _hover;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one Lamha may run. A second launch exits quietly.
        _singleInstance = new Mutex(initiallyOwned: true, "Lamha.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        CrashLog.Install(this);

        _panel = new PanelWindow();
        _hover = new HoverController(_panel);
        _tray = new TrayIconController(
            onHover: _hover.OnIconHover,
            onClick: _hover.OnIconClick,
            onExit: Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
