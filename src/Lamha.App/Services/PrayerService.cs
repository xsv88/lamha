using System.Globalization;
using System.Windows.Threading;
using Lamha.App.Tray;
using Lamha.Core.Prayer;
using Lamha.Core.Settings;
using Microsoft.Win32;

namespace Lamha.App.Services;

/// <summary>
/// Keeps the prayer state up to date: every second while the panel is open,
/// every minute while it is closed (enough for the icon color and alerts).
/// </summary>
public sealed class PrayerService : IDisposable
{
    private static readonly UmAlQuraCalendar Hijri = new();

    private readonly DispatcherTimer _timer = new();
    private readonly Dictionary<DateOnly, PrayerTimes> _cache = new();
    private PrayerSettings _settings;
    private (double Lat, double Lon) _point = LocationService.Dammam;

    public PrayerService(PrayerSettings settings)
    {
        _settings = settings;
        _timer.Tick += (_, _) => Tick();
        SetPanelOpen(false);

        // Recalculate after sleep, clock change or time zone change.
        SystemEvents.TimeChanged += OnSystemChange;
        SystemEvents.PowerModeChanged += OnPowerChange;
    }

    /// <summary>Raised on every tick with the current state and today's six times.</summary>
    public event Action<PrayerState, PrayerTimes>? Updated;

    /// <summary>Raised when the tray color should change.</summary>
    public event Action<TrayAccent>? AccentChanged;

    public PrayerState? Current { get; private set; }

    private bool _panelOpen, _stripNeedsSeconds;

    public void SetPanelOpen(bool open)
    {
        _panelOpen = open;
        UpdateInterval();
    }

    /// <summary>The taskbar strip shows the prayer countdown, so it needs a tick every second too.</summary>
    public void SetStripNeedsSeconds(bool needs)
    {
        _stripNeedsSeconds = needs;
        UpdateInterval();
    }

    private void UpdateInterval()
    {
        _timer.Interval = _panelOpen || _stripNeedsSeconds ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1);
        _timer.Start();
        Tick();
    }

    public void SetLocation((double Lat, double Lon) point)
    {
        _point = point;
        Invalidate();
    }

    public void SetSettings(PrayerSettings settings)
    {
        _settings = settings;
        Invalidate();
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.TimeChanged -= OnSystemChange;
        SystemEvents.PowerModeChanged -= OnPowerChange;
    }

    private void OnSystemChange(object? s, EventArgs e) => Invalidate();

    private void OnPowerChange(object? s, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Invalidate();
    }

    private void Invalidate()
    {
        _cache.Clear();
        TimeZoneInfo.ClearCachedData();
        Tick();
    }

    private void Tick()
    {
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.DateTime);
        var state = PrayerStateMachine.Evaluate(now, TimesFor(today), TimesFor(today.AddDays(1)),
            _settings.IqamaMinutes, TimesFor(today.AddDays(-1)));

        Current = state;
        Updated?.Invoke(state, TimesFor(today));
        AccentChanged?.Invoke(PrayerStateMachine.AccentFor(state) switch
        {
            TrayAccentHint.Blue => TrayAccent.Blue,
            TrayAccentHint.Yellow => TrayAccent.Yellow,
            TrayAccentHint.Red => TrayAccent.Red,
            _ => TrayAccent.None,
        });
    }

    private PrayerTimes TimesFor(DateOnly date)
    {
        if (_cache.TryGetValue(date, out var cached)) return cached;
        if (_cache.Count > 7) _cache.Clear();

        bool ramadan = Hijri.GetMonth(date.ToDateTime(TimeOnly.MinValue)) == 9;
        var times = PrayerCalculator.Calculate(date, LocationService.For(_point, date),
            _settings.OffsetMinutes, ramadan, _settings.Asr);
        return _cache[date] = times;
    }
}
