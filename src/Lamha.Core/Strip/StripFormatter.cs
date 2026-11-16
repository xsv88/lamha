using Lamha.Core.Prayer;

namespace Lamha.Core.Strip;

/// <summary>Turns live data into the two short lines shown on the taskbar.</summary>
public static class StripFormatter
{
    private const int MaxTitle = 18;
    private static readonly TimeSpan BlueWindow = TimeSpan.FromMinutes(15);

    /// <param name="inWord">"in" / "بعد".</param>
    /// <param name="iqamaWord">"Iqama" / "الإقامة".</param>
    public static StripText NextPrayer(PrayerState s, Func<PrayerName, string> name, string inWord, string iqamaWord)
    {
        string n = name(s.Target);
        return s.Phase switch
        {
            PrayerPhase.IqamaWait => new($"{n}", $"{iqamaWord} {Mmss(s.Remaining)}", StripTone.Yellow),
            PrayerPhase.LastMinute => new($"{n}", $"{iqamaWord} {Mmss(s.Remaining)}", StripTone.Red),
            _ => new($"{n} {s.TargetTime:HH:mm}", $"{inWord} {(int)s.Remaining.TotalHours}:{s.Remaining.Minutes:00}",
                s.Target != PrayerName.Shuruq && s.Remaining < BlueWindow ? StripTone.Blue : StripTone.Normal),
        };
    }

    public static StripText NowPlaying(string title, string artist) => new(Cut(title), Cut(artist));

    public static StripText CpuRam(double cpuPercent, double ramUsedGb) =>
        new($"CPU {cpuPercent:0}%", $"RAM {ramUsedGb:0.0} GB");

    public static StripText Hijri(int day, string month, int year) => new($"{day} {month}", $"{year}");

    private static string Cut(string s) => s.Length <= MaxTitle ? s : s[..MaxTitle] + "…";

    private static string Mmss(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
