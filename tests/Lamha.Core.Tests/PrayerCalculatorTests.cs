using Lamha.Core.Prayer;

namespace Lamha.Core.Tests;

public class PrayerCalculatorTests
{
    // Dammam, UTC+3. Reference times are published Umm al-Qura timetables:
    // 2026-07-02 from edarabia.com/prayer-times-dammam, 2026-08-12 from saudilifeguide.com.
    private static readonly Location Dammam = new(26.4207, 50.0888, TimeSpan.FromHours(3));
    private static readonly Dictionary<PrayerName, int> NoOffsets = new();

    public static TheoryData<string, string, string, string, string, string, string> References => new()
    {
        { "2026-07-02", "03:19", "04:52", "11:45", "15:11", "18:38", "20:08" },
        { "2026-08-12", "03:45", "05:10", "11:45", "15:16", "18:19", "19:49" },
    };

    [Theory]
    [MemberData(nameof(References))]
    public void Calculate_Dammam_MatchesReference(
        string date, string fajr, string shuruq, string dhuhr, string asr, string maghrib, string isha)
    {
        var d = DateOnly.Parse(date);
        var times = PrayerCalculator.Calculate(d, Dammam, NoOffsets, isRamadan: false);

        string[] expected = [fajr, shuruq, dhuhr, asr, maghrib, isha];
        foreach (var (name, hhmm) in Enum.GetValues<PrayerName>().Zip(expected))
        {
            var want = new DateTimeOffset(d.ToDateTime(TimeOnly.Parse(hhmm)), Dammam.UtcOffset);
            var got = times.Times[name];
            Assert.NotNull(got);
            Assert.True(Math.Abs((got!.Value - want).TotalMinutes) <= 2,
                $"{name}: expected ~{hhmm}, got {got.Value:HH:mm:ss}");
        }
    }

    [Fact]
    public void Calculate_Ramadan_IshaIs120AfterMaghrib()
    {
        var t = PrayerCalculator.Calculate(new DateOnly(2026, 8, 12), Dammam, NoOffsets, isRamadan: true);
        Assert.Equal(TimeSpan.FromMinutes(120), t.Times[PrayerName.Isha]!.Value - t.Times[PrayerName.Maghrib]!.Value);
    }

    [Fact]
    public void Calculate_Offsets_ShiftEachPrayer()
    {
        var d = new DateOnly(2026, 8, 12);
        var baseT = PrayerCalculator.Calculate(d, Dammam, NoOffsets, isRamadan: false);
        var shifted = PrayerCalculator.Calculate(d, Dammam, new Dictionary<PrayerName, int> { [PrayerName.Dhuhr] = 5 }, isRamadan: false);

        Assert.Equal(TimeSpan.FromMinutes(5), shifted.Times[PrayerName.Dhuhr]!.Value - baseT.Times[PrayerName.Dhuhr]!.Value);
        Assert.Equal(baseT.Times[PrayerName.Asr], shifted.Times[PrayerName.Asr]);
    }

    [Fact]
    public void Calculate_Offsets_ClampedToPlusMinus30()
    {
        var d = new DateOnly(2026, 8, 12);
        var baseT = PrayerCalculator.Calculate(d, Dammam, NoOffsets, isRamadan: false);
        var shifted = PrayerCalculator.Calculate(d, Dammam, new Dictionary<PrayerName, int> { [PrayerName.Fajr] = 45 }, isRamadan: false);

        Assert.Equal(TimeSpan.FromMinutes(30), shifted.Times[PrayerName.Fajr]!.Value - baseT.Times[PrayerName.Fajr]!.Value);
    }

    [Fact]
    public void Calculate_HighLatitude_FajrNullNotNaN()
    {
        var north = new Location(66.0, 25.0, TimeSpan.FromHours(3));
        var t = PrayerCalculator.Calculate(new DateOnly(2026, 6, 21), north, NoOffsets, isRamadan: false);

        Assert.Null(t.Times[PrayerName.Fajr]);
        Assert.NotNull(t.Times[PrayerName.Dhuhr]);
    }

    [Fact]
    public void Calculate_Hanafi_AsrLaterThanStandard()
    {
        var d = new DateOnly(2026, 8, 12);
        var std = PrayerCalculator.Calculate(d, Dammam, NoOffsets, false, AsrMethod.Standard);
        var han = PrayerCalculator.Calculate(d, Dammam, NoOffsets, false, AsrMethod.Hanafi);

        Assert.True(han.Times[PrayerName.Asr] > std.Times[PrayerName.Asr]);
    }
}
