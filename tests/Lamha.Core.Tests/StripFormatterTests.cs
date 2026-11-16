using Lamha.Core.Prayer;
using Lamha.Core.Strip;

namespace Lamha.Core.Tests;

public class StripFormatterTests
{
    private static readonly DateTimeOffset Fajr = new(2026, 11, 17, 4, 37, 0, TimeSpan.FromHours(3));
    private static string Name(PrayerName p) => p.ToString();

    [Fact]
    public void Format_NextPrayer_NormalShowsTimeAndIn()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, Fajr, new TimeSpan(7, 10, 5)), Name, "in", "Iqama");
        Assert.Equal("Fajr 04:37", t.Line1);
        Assert.Equal("in 7:10", t.Line2);
        Assert.Equal(StripTone.Normal, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_Under15_Blue()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, Fajr, TimeSpan.FromMinutes(9.5)), Name, "in", "Iqama");
        Assert.Equal("in 0:09", t.Line2);
        Assert.Equal(StripTone.Blue, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_IqamaShowsIqama_Yellow()
    {
        var iqama = Fajr.AddMinutes(25);
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.IqamaWait, PrayerName.Fajr, iqama, new TimeSpan(0, 12, 40)), Name, "in", "Iqama");
        Assert.Equal("Fajr", t.Line1);
        Assert.Equal("Iqama 12:40", t.Line2);
        Assert.Equal(StripTone.Yellow, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_LastMinute_Red()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.LastMinute, PrayerName.Fajr, Fajr, TimeSpan.FromSeconds(42)), Name, "in", "Iqama");
        Assert.Equal("Iqama 0:42", t.Line2);
        Assert.Equal(StripTone.Red, t.Tone);
    }

    [Fact]
    public void Format_NowPlaying_LongTitleTruncated()
    {
        var t = StripFormatter.NowPlaying("I Built the MOST Overkill Iron Farm", "Zonkski");
        Assert.Equal("I Built the MOST O…", t.Line1);
        Assert.Equal("Zonkski", t.Line2);
    }

    [Fact]
    public void Format_CpuRam()
    {
        var t = StripFormatter.CpuRam(31.4, 26.06);
        Assert.Equal("CPU 31%", t.Line1);
        Assert.Equal("RAM 26.1 GB", t.Line2);
    }

    [Fact]
    public void Format_Hijri()
    {
        var t = StripFormatter.Hijri(6, "Jumada II", 1448);
        Assert.Equal("6 Jumada II", t.Line1);
        Assert.Equal("1448", t.Line2);
    }
}
