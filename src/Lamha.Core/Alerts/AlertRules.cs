using Lamha.Core.Prayer;
using Lamha.Core.Settings;

namespace Lamha.Core.Alerts;

/// <param name="Key">"yyyy-MM-dd:Prayer" for a normal reminder, "missed" for the catch-up after full screen.</param>
/// <param name="Due">The adhan time the reminder is about.</param>
public sealed record Alert(string Key, PrayerName Prayer, DateTimeOffset Due);

/// <summary>
/// Decides when to show a prayer reminder: once per prayer per day, N minutes before adhan.
/// While a full-screen app runs (and do-not-disturb is on), reminders wait and come back
/// as one "missed" reminder when full screen ends.
/// </summary>
public sealed class AlertRules(PrayerSettings settings, bool dndEnabled)
{
    private readonly HashSet<string> _sent = [];
    private Alert? _queued;
    private PrayerSettings _settings = settings;
    private bool _dnd = dndEnabled;

    public void Update(PrayerSettings settings, bool dndEnabled)
    {
        _settings = settings;
        _dnd = dndEnabled;
    }

    public IReadOnlyList<Alert> Tick(DateTimeOffset now, PrayerState state, bool fullscreenBusy)
    {
        bool hold = fullscreenBusy && _dnd;
        var result = new List<Alert>();

        // Full screen just ended: deliver what was held, as one reminder.
        if (!hold && _queued is not null)
        {
            result.Add(_queued with { Key = "missed" });
            _queued = null;
        }

        if (_settings.AlertsEnabled && Due(now, state) is { } alert && _sent.Add(alert.Key))
        {
            PruneOld(now);
            if (hold) _queued = alert;
            else if (result.Count == 0) result.Add(alert);
        }
        return result;
    }

    /// <summary>The reminder that should be showing now, if any.</summary>
    private Alert? Due(DateTimeOffset now, PrayerState s)
    {
        if (s.Target == PrayerName.Shuruq) return null;
        int before = _settings.AlertMinutesBefore;

        if (s.Phase == PrayerPhase.Normal && before > 0 && s.Remaining <= TimeSpan.FromMinutes(before) && s.Remaining > TimeSpan.Zero)
            return Make(s.Target, s.TargetTime);

        if (s.Phase != PrayerPhase.Normal && before == 0)
        {
            // In iqama phases TargetTime is the iqama; the adhan is that minus the gap.
            var adhan = s.TargetTime.AddMinutes(-_settings.IqamaMinutes.GetValueOrDefault(s.Target));
            return Make(s.Target, adhan);
        }
        return null;
    }

    private static Alert Make(PrayerName p, DateTimeOffset adhan) => new($"{adhan:yyyy-MM-dd}:{p}", p, adhan);

    private void PruneOld(DateTimeOffset now)
    {
        string keepFrom = now.AddDays(-1).ToString("yyyy-MM-dd");
        _sent.RemoveWhere(k => string.CompareOrdinal(k, keepFrom) < 0);
    }
}
