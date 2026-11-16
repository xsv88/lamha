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
    private readonly Providers.MediaProvider _media = new();
    private readonly Providers.SystemProvider _system = new();
    private readonly System.Windows.Threading.DispatcherTimer _systemTimer = new() { Interval = TimeSpan.FromSeconds(1) };
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

        // Now playing: listen only while the panel is open.
        _media.Changed += np => Dispatcher.BeginInvoke(() => _panel.NowPlaying.Show(np));
        _panel.NowPlaying.PlayPause += () => _ = _media.PlayPauseAsync();
        _panel.NowPlaying.Next += () => _ = _media.NextAsync();
        _panel.NowPlaying.Previous += () => _ = _media.PreviousAsync();
        _panel.OpenChanged += open =>
        {
            if (open) _ = _media.StartAsync();
            else _media.Stop();
        };

        // PC load: sample once a second, only while the panel is open.
        _systemTimer.Tick += (_, _) => _panel.PcLoad.Show(_system.Sample(_panel.PcLoad.WantsTopApps));
        _panel.OpenChanged += open =>
        {
            if (open) { _panel.PcLoad.Show(_system.Sample(_panel.PcLoad.WantsTopApps)); _systemTimer.Start(); }
            else _systemTimer.Stop();
        };

        _prayer.SetLocation(await LocationService.ResolveAsync(Settings.Location));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _prayer?.Dispose();
        _system.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
