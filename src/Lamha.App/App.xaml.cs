using System.Windows;
using Lamha.App.Panel;
using Lamha.App.Services;
using Lamha.Core.Settings;
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
    private PrayerService? _prayer;
    private SettingsStore? _store;

    public AppSettings Settings { get; private set; } = AppSettings.Default;

    protected override async void OnStartup(StartupEventArgs e)
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

        _store = new SettingsStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lamha", "settings.json"));
        Settings = _store.Load();

        _prayer = new PrayerService(Settings.Prayer);
        _prayer.AccentChanged += _tray.SetAccent;
        _prayer.Updated += (state, times) =>
        {
            if (!_panel.IsVisible) return; // No drawing while hidden.
            _panel.Header.Refresh();
            _panel.Prayer.Show(state, times);
        };
        _panel.OpenChanged += open => _prayer.SetPanelOpen(open);

        _prayer.SetLocation(await LocationService.ResolveAsync(Settings.Location));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _prayer?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
