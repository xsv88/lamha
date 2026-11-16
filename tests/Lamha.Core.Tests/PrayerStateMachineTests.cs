using Lamha.Core.Prayer;

namespace Lamha.Core.Tests;

public class PrayerStateMachineTests
{
    private static readonly TimeSpan Tz = TimeSpan.FromHours(3);
    private static readonly DateOnly Day = new(2026, 11, 16);

    private static readonly Dictionary<PrayerName, int> Iqama = new()
    {
        [PrayerName.Fajr] = 25, [PrayerName.Dhuhr] = 20, [PrayerName.Asr] = 20,
        [PrayerName.Maghrib] = 10, [PrayerName.Isha] = 20,
    };

    private static DateTimeOffset At(DateOnly d, string hhmmss) =>
        new(d.ToDateTime(TimeOnly.Parse(hhmmss)), Tz);

    private static PrayerTimes Times(DateOnly d, string? fajr = "04:40", string isha = "18:20") =>
        new(d, new Dictionary<PrayerName, DateTimeOffset?>
        {
            [PrayerName.Fajr] = fajr is null ? null : At(d, fajr),
            [PrayerName.Shuruq] = At(d, "06:00"),
            [PrayerName.Dhuhr] = At(d, "11:52"),
            [PrayerName.Asr] = At(d, "14:54"),
            [PrayerName.Maghrib] = At(d, "16:50"),
            [PrayerName.Isha] = At(d, isha),
        });

    private static PrayerState Eval(string now, PrayerTimes? today = null) =>
        PrayerStateMachine.Evaluate(At(Day, now), today ?? Times(Day), Times(Day.AddDays(1)), Iqama);

    [Fact]
    public void State_BeforeAdhan_NormalCountsToAdhan()
    {
        var s = Eval("11:40:00");
        Assert.Equal(PrayerPhase.Normal, s.Phase);
        Assert.Equal(PrayerName.Dhuhr, s.Target);
        Assert.Equal(TimeSpan.FromMinutes(12), s.Remaining);
        Assert.Equal(TrayAccentHint.Blue, PrayerStateMachine.AccentFor(s));
    }

    [Fact]
    public void State_ThirtyMinBefore_NoAccent() =>
        Assert.Equal(TrayAccentHint.None, PrayerStateMachine.AccentFor(Eval("11:22:00")));

    [Fact]
    public void State_AtAdhan_IqamaWait()
    {
        var s = Eval("11:52:00");
        Assert.Equal(PrayerPhase.IqamaWait, s.Phase);
        Assert.Equal(PrayerName.Dhuhr, s.Target);
        Assert.Equal(TimeSpan.FromMinutes(20), s.Remaining);
        Assert.Equal(TrayAccentHint.Yellow, PrayerStateMachine.AccentFor(s));
    }

    [Fact]
    public void State_61sBeforeIqama_StillYellow() =>
        Assert.Equal(PrayerPhase.IqamaWait, Eval("12:10:59").Phase);

    [Fact]
    public void State_60sBeforeIqama_Red()
    {
        var s = Eval("12:11:00");
        Assert.Equal(PrayerPhase.LastMinute, s.Phase);
        Assert.Equal(TrayAccentHint.Red, PrayerStateMachine.AccentFor(s));
    }

    [Fact]
    public void State_AtIqama_BackToNormalNextPrayer()
    {
        var s = Eval("12:12:00");
        Assert.Equal(PrayerPhase.Normal, s.Phase);
        Assert.Equal(PrayerName.Asr, s.Target);
    }

    [Fact]
    public void State_ShuruqNeverYellow()
    {
        var s = Eval("06:01:00");
        Assert.Equal(PrayerPhase.Normal, s.Phase);
        Assert.Equal(PrayerName.Dhuhr, s.Target);
    }

    [Fact]
    public void State_BeforeShuruq_NoAccent() =>
        Assert.Equal(TrayAccentHint.None, PrayerStateMachine.AccentFor(Eval("05:55:00")));

    [Fact]
    public void State_AfterMidnight_UsesNextDayFajr()
    {
        var s = Eval("23:59:00");
        Assert.Equal(PrayerName.Fajr, s.Target);
        Assert.Equal(At(Day.AddDays(1), "04:40"), s.TargetTime);
    }

    [Fact]
    public void State_IshaIqamaCrossesMidnight()
    {
        var yesterday = Times(Day.AddDays(-1), isha: "23:50");
        var s = PrayerStateMachine.Evaluate(At(Day, "00:05:00"), Times(Day), Times(Day.AddDays(1)), Iqama, yesterday);
        Assert.Equal(PrayerPhase.IqamaWait, s.Phase);
        Assert.Equal(PrayerName.Isha, s.Target);
        Assert.Equal(TimeSpan.FromMinutes(5), s.Remaining);
    }

    [Fact]
    public void State_NullPrayerSkipped()
    {
        var s = Eval("03:00:00", Times(Day, fajr: null));
        Assert.Equal(PrayerName.Shuruq, s.Target);
    }
}
