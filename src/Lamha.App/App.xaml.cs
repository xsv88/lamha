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
    private HotkeyService? _hotkey;
    private Lamha.Core.Alerts.AlertRules? _alerts;
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
            onSettings: () => OpenSettings(),
            onExit: Shutdown);

        _store = new SettingsStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lamha", "settings.json"));
        Settings = _store.Load();
        _panel.ApplySettings(Settings);
        StartupService.SetEnabled(Settings.StartWithWindows);

        _prayer = new PrayerService(Settings.Prayer);
        _prayer.AccentChanged += _tray.SetAccent;
        _alerts = new Lamha.Core.Alerts.AlertRules(Settings.Prayer, Settings.DoNotDisturbFullscreen);
        _prayer.Updated += (state, times) =>
        {
            foreach (var alert in _alerts.Tick(DateTimeOffset.Now, state, Providers.FullscreenDetector.IsBusy()))
            {
                if (alert.Key == "missed")
                    ToastService.Show("Prayer reminder", $"You were in full screen. {alert.Prayer} at {alert.Due:HH:mm}.");
                else
                    ToastService.Show($"{alert.Prayer} at {alert.Due:HH:mm}",
                        $"In {Math.Max(0, (int)Math.Round((alert.Due - DateTimeOffset.Now).TotalMinutes))} minutes.");
            }

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

        _panel.Prayer.Clicked += () => OpenSettings(prayerTab: true);
        _panel.PcLoad.Clicked += () =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
            catch (Exception ex) { CrashLog.Write(ex); }
        };

        _hotkey = new HotkeyService();
        if (!_hotkey.Register(Settings.Hotkey, _hover.TogglePinned))
            CrashLog.Write(new InvalidOperationException($"Hotkey {Settings.Hotkey} is in use by another app."));

        _prayer.SetLocation(await LocationService.ResolveAsync(Settings.Location));
    }

    private Settings.SettingsWindow? _settingsWindow;

    /// <summary>Raised after settings are saved, for services added in later tasks.</summary>
    public event Action<AppSettings>? SettingsChanged;

    private void OpenSettings(bool prayerTab = false)
    {
        _panel?.HidePanel();
        if (_settingsWindow is null)
        {
            _settingsWindow = new Settings.SettingsWindow(Settings);
            _settingsWindow.Changed += ApplySettings;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        if (prayerTab) _settingsWindow.OpenPrayerTab();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async void ApplySettings(AppSettings next)
    {
        var previous = Settings;
        Settings = next.Normalized();

        if (previous.Hotkey != Settings.Hotkey && _hotkey is not null && _hover is not null
            && !_hotkey.Register(Settings.Hotkey, _hover.TogglePinned))
        {
            // Keep the old shortcut and tell the user.
            Settings = Settings with { Hotkey = previous.Hotkey };
            _hotkey.Register(previous.Hotkey, _hover.TogglePinned);
            _settingsWindow?.ShowHotkeyStatus("This shortcut is in use. Kept " + previous.Hotkey + ".");
        }
        _store?.Save(Settings);

        _panel?.ApplySettings(Settings);
        _prayer?.SetSettings(Settings.Prayer);
        _alerts?.Update(Settings.Prayer, Settings.DoNotDisturbFullscreen);
        if (previous.StartWithWindows != Settings.StartWithWindows) StartupService.SetEnabled(Settings.StartWithWindows);
        if (previous.Location != Settings.Location && _prayer is not null)
            _prayer.SetLocation(await LocationService.ResolveAsync(Settings.Location));

        SettingsChanged?.Invoke(Settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _prayer?.Dispose();
        _hotkey?.Dispose();
        _system.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
