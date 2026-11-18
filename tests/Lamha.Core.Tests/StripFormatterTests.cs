using Lamha.Core.Prayer;
using Lamha.Core.Strip;

namespace Lamha.Core.Tests;

public class StripFormatterTests
{
    private static readonly DateTimeOffset Fajr = new(2026, 11, 17, 4, 37, 0, TimeSpan.FromHours(3));
    private static string Name(PrayerName p) => p.ToString();
    private static readonly StripWords Words = new(In: "in", Iqama: "Iqama", Min: "min", Hour: "h", MinuteShort: "m");

    [Fact]
    public void Format_NextPrayer_NormalShowsTimeAndIn()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, Fajr, new TimeSpan(7, 10, 5)), Name, Words);
        Assert.Equal("Fajr 04:37", t.Line1);
        Assert.Equal("in 7h 10m", t.Line2);
        Assert.Equal(StripTone.Normal, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_Under15_Blue()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, Fajr, TimeSpan.FromMinutes(9.5)), Name, Words);
        Assert.Equal("in 10 min", t.Line2); // Rounded up: never shows "0 min" while time is left.
        Assert.Equal(StripTone.Blue, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_IqamaShowsIqama_Yellow()
    {
        var iqama = Fajr.AddMinutes(25);
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.IqamaWait, PrayerName.Fajr, iqama, new TimeSpan(0, 12, 40)), Name, Words);
        Assert.Equal("Fajr", t.Line1);
        Assert.Equal("Iqama 12:40", t.Line2);
        Assert.Equal(StripTone.Yellow, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_LastMinute_Red()
    {
        var t = StripFormatter.NextPrayer(new PrayerState(PrayerPhase.LastMinute, PrayerName.Fajr, Fajr, TimeSpan.FromSeconds(42)), Name, Words);
        Assert.Equal("Iqama 0:42", t.Line2);
        Assert.Equal(StripTone.Red, t.Tone);
    }

    [Fact]
    public void Format_NextPrayer_ExactlyOneHour_ShowsHours() =>
        Assert.Equal("in 1h 0m", StripFormatter.NextPrayer(
            new PrayerState(PrayerPhase.Normal, PrayerName.Fajr, Fajr, TimeSpan.FromHours(1)), Name, Words).Line2);

    [Fact]
    public void Format_NowPlaying_LongTitleTruncated()
    {
        var t = StripFormatter.NowPlaying("I Built the MOST Overkill Iron Farm", "Zonkski", null, 0, playing: true);
        Assert.Equal("I Built the MOST O…", t.Line1);
        Assert.Equal("Zonkski", t.Line2);
        Assert.True(t.Playing);
    }

    [Fact]
    public void Format_NowPlaying_EmojiNotCutInHalf()
    {
        var t = StripFormatter.NowPlaying("12345678901234567🎵🎵", "", null, 0, playing: false);
        Assert.Equal("12345678901234567🎵…", t.Line1);
    }

    [Fact]
    public void Format_NowPlaying_PillColorReadableWithWhiteText()
    {
        var w = 4; var px = new byte[w * w * 4];
        for (int i = 0; i < px.Length; i += 4) { px[i] = 120; px[i + 1] = 230; px[i + 2] = 250; px[i + 3] = 255; } // Light yellow.
        var t = StripFormatter.NowPlaying("Song", "Artist", px, w, playing: true);
        Assert.NotNull(t.Pill);
        Assert.True(Lamha.Core.Media.AlbumColor.ContrastRatio(t.Pill!.Value, new Lamha.Core.Media.Rgb(255, 255, 255)) >= 4.5);
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

public class StripBatteryFormatTests
{
    [Fact]
    public void Format_Battery_LowIsRed()
    {
        var t = Lamha.Core.Strip.StripFormatter.Battery(new Lamha.Core.Batteries.BatteryReading("Galaxy Buds2 Pro", Lamha.Core.Batteries.DeviceKind.Headset, 15));
        Assert.Equal("Galaxy Buds2 Pro", t.Line1);
        Assert.Equal("15%", t.Line2);
        Assert.Equal(Lamha.Core.Strip.StripTone.Red, t.Tone);
    }

    [Fact]
    public void Format_Battery_OkIsNormal() =>
        Assert.Equal(Lamha.Core.Strip.StripTone.Normal, Lamha.Core.Strip.StripFormatter.Battery(
            new Lamha.Core.Batteries.BatteryReading("Mouse", Lamha.Core.Batteries.DeviceKind.Mouse, 64)).Tone);
}
