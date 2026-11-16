using System.Windows;
using Lamha.App.Panel;
using Lamha.App.Services;
using Lamha.Core.Settings;
using Lamha.App.Tray;
using Lamha.App.Strip;
using Lamha.Core.Prayer;
using Lamha.Core.Strip;

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
    private StripWindow? _strip;

    // Latest values, kept so the taskbar strip can show them while the panel is closed.
    private PrayerState? _lastPrayer;
    private Providers.NowPlaying? _lastNowPlaying;
    private Providers.SystemSnapshot? _lastSystem;

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
        Ui.Loc.Set(Settings.Language);
        _tray.Relabel();
        _panel.ApplySettings(Settings);
        StartupService.SetEnabled(Settings.StartWithWindows);

        _prayer = new PrayerService(Settings.Prayer);
        _prayer.AccentChanged += _tray.SetAccent;
        _alerts = new Lamha.Core.Alerts.AlertRules(Settings.Prayer, Settings.DoNotDisturbFullscreen);
        _prayer.Updated += (state, times) =>
        {
            foreach (var alert in _alerts.Tick(DateTimeOffset.Now, state, Providers.FullscreenDetector.IsBusy()))
            {
                string name = Ui.Loc.Prayer(alert.Prayer), time = alert.Due.ToString("HH:mm");
                if (alert.Key == "missed")
                    ToastService.Show(Ui.Loc.T("ToastMissedTitle"), string.Format(Ui.Loc.T("ToastMissedBody"), name, time));
                else
                    ToastService.Show(string.Format(Ui.Loc.T("ToastSoonTitle"), name, time),
                        string.Format(Ui.Loc.T("ToastSoonBody"), Math.Max(0, (int)Math.Round((alert.Due - DateTimeOffset.Now).TotalMinutes))));
            }

            _lastPrayer = state;
            RenderStrip();

            if (!_panel.IsVisible) return; // No drawing while hidden.
            _panel.Header.Refresh();
            _panel.Prayer.Show(state, times);
        };
        _panel.OpenChanged += open => _prayer.SetPanelOpen(open);

        // Now playing: listen while the panel is open, or while the strip shows it.
        _media.Changed += np => Dispatcher.BeginInvoke(() =>
        {
            _lastNowPlaying = np;
            RenderStrip();
            if (_panel.IsVisible) _panel.NowPlaying.Show(np); // Late events after closing are ignored.
        });
        _panel.NowPlaying.PlayPause += () => _ = _media.PlayPauseAsync();
        _panel.NowPlaying.Next += () => _ = _media.NextAsync();
        _panel.NowPlaying.Previous += () => _ = _media.PreviousAsync();
        _panel.OpenChanged += open =>
        {
            if (!open) _panel.NowPlaying.Show(null); // Stops the progress timer and the bar animations.
            UpdateActivity();
        };

        // PC load: sample once a second while the panel is open or the strip shows it.
        _systemTimer.Tick += (_, _) => SampleSystem();

        // Taskbar strip.
        _strip = new StripWindow();
        _hover.AttachStrip(_strip.ContainsCursor);
        _strip.Hovered += _hover.OnStripHover;
        _strip.Clicked += _hover.OnStripClick;
        _strip.OffsetChanged += offset => ApplySettings(Settings with { Strip = Settings.Strip with { OffsetFromRightPx = offset } });
        ApplyStripSettings();

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

    private bool _sampling;

    /// <summary>Reads counters on a background thread so the panel never stutters.</summary>
    private async void SampleSystem()
    {
        if (_sampling || _panel is null) return;
        _sampling = true;
        try
        {
            bool top = _panel.PcLoad.WantsTopApps;
            var snapshot = await Task.Run(() => _system.Sample(top && _panel.IsVisible));
            _lastSystem = snapshot;
            RenderStrip();
            if (_panel.IsVisible) _panel.PcLoad.Show(snapshot);
        }
        catch (Exception ex) { CrashLog.Write(ex); }
        finally { _sampling = false; }
    }

    private bool StripShows(StripItem item) => Settings.Strip.Enabled && Settings.Strip.Items.Contains(item);

    /// <summary>Starts or stops the media listener and PC sampling depending on who needs them.</summary>
    private void UpdateActivity()
    {
        if (_panel is null) return;
        bool panelOpen = _panel.IsVisible;

        if (panelOpen || StripShows(StripItem.NowPlaying)) _ = _media.StartAsync();
        else { _media.Stop(); _lastNowPlaying = null; }

        if (panelOpen || StripShows(StripItem.CpuRam)) { if (!_systemTimer.IsEnabled) { SampleSystem(); _systemTimer.Start(); } }
        else _systemTimer.Stop();

        _prayer?.SetStripNeedsSeconds(StripShows(StripItem.NextPrayer));
    }

    private void ApplyStripSettings()
    {
        _strip?.Configure(Settings.Strip.Enabled, Settings.Strip.OffsetFromRightPx);
        UpdateActivity();
        RenderStrip();
    }

    /// <summary>Builds the strip's text from the latest values. Items with no data yet are skipped.</summary>
    private void RenderStrip()
    {
        if (_strip is null || !Settings.Strip.Enabled) return;
        var texts = new List<StripText>();
        foreach (var item in Settings.Strip.Items)
        {
            switch (item)
            {
                case StripItem.NextPrayer when _lastPrayer is { } p:
                    texts.Add(StripFormatter.NextPrayer(p, Ui.Loc.Prayer, Ui.Loc.T("In"), Ui.Loc.T("Iqama")));
                    break;
                case StripItem.NowPlaying when _lastNowPlaying is { } np:
                    texts.Add(StripFormatter.NowPlaying(np.Title, np.Artist));
                    break;
                case StripItem.CpuRam when _lastSystem is { } sys:
                    texts.Add(StripFormatter.CpuRam(sys.CpuPercent, sys.RamUsedGb));
                    break;
                case StripItem.HijriDate:
                    var cal = new System.Globalization.UmAlQuraCalendar();
                    var now = DateTime.Now;
                    texts.Add(StripFormatter.Hijri(cal.GetDayOfMonth(now), Ui.Loc.HijriMonth(cal.GetMonth(now)), cal.GetYear(now)));
                    break;
            }
        }
        _strip.ShowItems(texts);
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
            _settingsWindow?.ShowHotkeyStatus(string.Format(Ui.Loc.T("HotkeyInUse"), previous.Hotkey));
        }
        _store?.Save(Settings);

        Ui.Loc.Set(Settings.Language);
        _tray?.Relabel();
        _panel?.ApplySettings(Settings);
        _prayer?.SetSettings(Settings.Prayer);
        _alerts?.Update(Settings.Prayer, Settings.DoNotDisturbFullscreen);
        if (previous.StartWithWindows != Settings.StartWithWindows) StartupService.SetEnabled(Settings.StartWithWindows);
        if (previous.Strip != Settings.Strip || previous.Language != Settings.Language) ApplyStripSettings();
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
