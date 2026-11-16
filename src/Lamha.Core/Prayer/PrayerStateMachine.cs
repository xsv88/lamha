namespace Lamha.Core.Prayer;

/// <summary>
/// Decides the prayer card state for a moment in time.
/// Pure function: same inputs always give the same state.
/// </summary>
public static class PrayerStateMachine
{
    private static readonly TimeSpan LastMinute = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AccentWindow = TimeSpan.FromMinutes(15);

    /// <param name="yesterday">Needed only after midnight, when last night's Isha iqama has not passed yet.</param>
    public static PrayerState Evaluate(
        DateTimeOffset now,
        PrayerTimes today,
        PrayerTimes tomorrow,
        IReadOnlyDictionary<PrayerName, int> iqamaMinutes,
        PrayerTimes? yesterday = null)
    {
        var days = new[] { yesterday, today, tomorrow }.OfType<PrayerTimes>().ToList();
        var events = days
            .SelectMany(d => d.Times)
            .Where(kv => kv.Value is not null)
            .Select(kv => (Name: kv.Key, Adhan: kv.Value!.Value))
            .OrderBy(e => e.Adhan)
            .ToList();

        // 1) Are we between an adhan and its iqama?
        foreach (var (name, adhan) in events)
        {
            if (name == PrayerName.Shuruq) continue; // Shuruq has no iqama.
            var iqama = adhan.AddMinutes(iqamaMinutes.GetValueOrDefault(name));
            if (now >= adhan && now < iqama)
            {
                var left = iqama - now;
                var phase = left <= LastMinute ? PrayerPhase.LastMinute : PrayerPhase.IqamaWait;
                return new PrayerState(phase, name, iqama, left);
            }
        }

        // 2) Otherwise count down to the next adhan (Shuruq included, shown in the row).
        var next = events.FirstOrDefault(e => e.Adhan > now);
        if (next == default)
        {
            // Only possible if every time is null (extreme latitude). Show "nothing" safely.
            return new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, now, TimeSpan.Zero);
        }
        return new PrayerState(PrayerPhase.Normal, next.Name, next.Adhan, next.Adhan - now);
    }

    /// <summary>Tray color: blue when a prayer (not Shuruq) is under 15 minutes away, yellow/red while waiting for iqama.</summary>
    public static TrayAccentHint AccentFor(PrayerState s) => s.Phase switch
    {
        PrayerPhase.IqamaWait => TrayAccentHint.Yellow,
        PrayerPhase.LastMinute => TrayAccentHint.Red,
        _ when s.Target != PrayerName.Shuruq && s.Remaining < AccentWindow => TrayAccentHint.Blue,
        _ => TrayAccentHint.None,
    };
}
