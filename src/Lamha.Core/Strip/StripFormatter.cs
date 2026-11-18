using System.Globalization;
using Lamha.Core.Batteries;
using Lamha.Core.Media;
using Lamha.Core.Prayer;

namespace Lamha.Core.Strip;

/// <summary>Turns live data into the short lines shown on the taskbar.</summary>
public static class StripFormatter
{
    private const int MaxTitle = 18;
    private static readonly TimeSpan BlueWindow = TimeSpan.FromMinutes(15);

    public static StripText NextPrayer(PrayerState s, Func<PrayerName, string> name, StripWords w)
    {
        string n = name(s.Target);
        return s.Phase switch
        {
            PrayerPhase.IqamaWait => new(n, $"{w.Iqama} {Mmss(s.Remaining)}", StripTone.Yellow),
            PrayerPhase.LastMinute => new(n, $"{w.Iqama} {Mmss(s.Remaining)}", StripTone.Red),
            _ => new($"{n} {s.TargetTime:HH:mm}", $"{w.In} {Until(s.Remaining, w)}",
                s.Target != PrayerName.Shuruq && s.Remaining < BlueWindow ? StripTone.Blue : StripTone.Normal),
        };
    }

    /// <param name="coverBgra">Album cover pixels (square), or null.</param>
    public static StripText NowPlaying(string title, string artist, byte[]? coverBgra, int coverSize, bool playing)
    {
        var color = coverBgra is null ? AlbumColor.Neutral : AlbumColor.Dominant(coverBgra, coverSize, coverSize);
        return new StripText(Cut(title), Cut(artist))
        {
            Pill = AlbumColor.EnsureContrastWithWhite(color),
            Image = coverBgra,
            ImageSize = coverSize,
            Playing = playing,
        };
    }

    public static StripText CpuRam(double cpuPercent, double ramUsedGb) =>
        new($"CPU {cpuPercent:0}%", $"RAM {ramUsedGb:0.0} GB");

    /// <summary>The device most in need of charging: name, then percent; red when low.</summary>
    public static StripText Battery(BatteryReading r) =>
        new(Cut(r.Name), $"{r.Percent}%", r.IsLow ? StripTone.Red : StripTone.Normal);

    public static StripText Hijri(int day, string month, int year) => new($"{day} {month}", $"{year}");

    /// <summary>"10 min" under an hour (rounded up, so it never says 0 while time is left), else "7h 10m".</summary>
    private static string Until(TimeSpan t, StripWords w)
    {
        if (t < TimeSpan.FromHours(1)) return $"{(int)Math.Ceiling(t.TotalMinutes)} {w.Min}";
        return $"{(int)t.TotalHours}{w.Hour} {t.Minutes}{w.MinuteShort}";
    }

    /// <summary>Cuts by visible characters, so an emoji is never split into a broken box.</summary>
    private static string Cut(string s)
    {
        var info = new StringInfo(s);
        return info.LengthInTextElements <= MaxTitle ? s : info.SubstringByTextElements(0, MaxTitle) + "…";
    }

    private static string Mmss(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
