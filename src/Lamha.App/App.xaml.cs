using System.Windows;
using Lamha.App.Panel;
using Lamha.App.Services;
using Lamha.Core.Settings;
using Lamha.App.Tray;
using Lamha.App.Strip;
using Lamha.Core.Prayer;
using Lamha.Core.Strip;
using Lamha.Core.Batteries;

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
    private readonly MemoryTrimmer _trimmer = new();
    private readonly Providers.BatteryProvider _batteries = new();
    private readonly LowBatteryAlerts _batteryAlerts = new();
    private readonly System.Windows.Threading.DispatcherTimer _batteryTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private IReadOnlyList<BatteryReading> _lastBatteries = [];
    private TrayAccent _prayerAccent = TrayAccent.None;
    private bool _readingBatteries;

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
        _prayer.AccentChanged += accent => { _prayerAccent = accent; UpdateAccent(); };
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
            else _panel.Batteries.Show(_lastBatteries);
            UpdateActivity();
            if (open) _trimmer.Cancel(); else _trimmer.Schedule();
        };

        // Batteries: every 30 s while shown (panel or strip), every 5 min otherwise for low-battery alerts.
        _batteryTimer.Tick += (_, _) => ReadBatteries();
        _batteryTimer.Start();
        ReadBatteries();

        // PC load: sample once a second while the panel is open or the strip shows it.
        _systemTimer.Tick += (_, _) => SampleSystem();

        // Taskbar strip.
        _strip = new StripWindow();
        _hover.AttachStrip(_strip.ContainsCursor);
        _strip.Hovered += _hover.OnStripHover;
        _strip.Clicked += _hover.OnStripClick;
        _strip.OffsetChanged += SaveStripOffset;
        _strip.SuppressedChanged += UpdateActivity;
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
        _trimmer.Schedule(); // Startup allocates a lot once; give it back.
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

    /// <summary>True when the strip is on, shows this item, and is not hidden by a game, auto-hide or a vertical taskbar.</summary>
    private bool StripShows(StripItem item) =>
        Settings.Strip.Enabled && Settings.Strip.Items.Contains(item) && _strip is { SuppressedByEnvironment: false };

    /// <summary>Drag ended: only the offset changes, so save it without re-applying everything.</summary>
    private void SaveStripOffset(double offset)
    {
        Settings = Settings with { Strip = Settings.Strip with { OffsetFromRightPx = offset } };
        try { _store?.Save(Settings); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

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

        var batteryInterval = panelOpen || StripShows(StripItem.Battery) ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);
        if (_batteryTimer.Interval != batteryInterval)
        {
            _batteryTimer.Interval = batteryInterval;
            if (panelOpen) ReadBatteries(); // Fresh numbers right when the panel opens.
        }
    }

    /// <summary>Reads all battery sources, then updates panel, strip, tray color and alerts.</summary>
    private async void ReadBatteries()
    {
        if (_readingBatteries) return;
        _readingBatteries = true;
        try
        {
            _lastBatteries = await _batteries.ReadAsync();

            // Same do-not-disturb rule as prayer reminders: wait until full screen ends.
            if (!(Settings.DoNotDisturbFullscreen && Providers.FullscreenDetector.IsBusy()))
            {
                foreach (var low in _batteryAlerts.Tick(_lastBatteries))
                {
                    string name = low.Kind == DeviceKind.ThisPc ? Ui.Loc.T("ThisPc") : low.Name;
                    ToastService.Show(string.Format(Ui.Loc.T("BatteryLowTitle"), name),
                        string.Format(Ui.Loc.T("BatteryLowBody"), low.Percent));
                }
            }

            if (_panel is { IsVisible: true }) _panel.Batteries.Show(_lastBatteries);
            RenderStrip();
            UpdateAccent();
        }
        catch (Exception ex) { CrashLog.Write(ex); }
        finally { _readingBatteries = false; }
    }

    /// <summary>Prayer colors win; otherwise blue when any device battery is low.</summary>
    private void UpdateAccent()
    {
        var accent = _prayerAccent != TrayAccent.None ? _prayerAccent
            : _lastBatteries.Any(b => b.IsLow) ? TrayAccent.Blue
            : TrayAccent.None;
        _tray?.SetAccent(accent);
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
                    texts.Add(StripFormatter.NextPrayer(p, Ui.Loc.Prayer, Ui.Loc.StripWords));
                    break;
                case StripItem.NowPlaying when _lastNowPlaying is { } np:
                    texts.Add(StripFormatter.NowPlaying(np.Title, np.Artist, np.ThumbnailBgra, np.ThumbW, np.IsPlaying));
                    break;
                case StripItem.CpuRam when _lastSystem is { } sys:
                    texts.Add(StripFormatter.CpuRam(sys.CpuPercent, sys.RamUsedGb));
                    break;
                case StripItem.Battery when BatteryRules.Lowest(_lastBatteries) is { } low:
                    texts.Add(StripFormatter.Battery(low.Kind == DeviceKind.ThisPc ? low with { Name = Ui.Loc.T("ThisPc") } : low));
                    break;
                case StripItem.HijriDate:
                    var cal = new System.Globalization.UmAlQuraCalendar();
                    var now = DateTime.Now;
                    texts.Add(StripFormatter.Hijri(cal.GetDayOfMonth(now), Ui.Loc.HijriMonthShort(cal.GetMonth(now)), cal.GetYear(now)));
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
            // The settings window may hold an old strip offset (the user can drag while it is open):
            // keep the current one unless the window asked to reset it.
            _settingsWindow.Changed += (next, resetStrip) => ApplySettings(next with
            {
                Strip = next.Strip with { OffsetFromRightPx = resetStrip ? null : Settings.Strip.OffsetFromRightPx },
            });
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
        if (!previous.Strip.SameAs(Settings.Strip) || previous.Language != Settings.Language) ApplyStripSettings();
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
