using Lamha.Core.Settings;
using Lamha.Core.Strip;

namespace Lamha.Core.Tests;

public class StripSettingsTests
{
    private static AppSettings With(params StripItem[] items) =>
        AppSettings.Default with { Strip = new StripSettings { Items = items } };

    [Fact]
    public void Defaults_StripOnWithNextPrayer()
    {
        Assert.True(AppSettings.Default.Strip.Enabled);
        Assert.Equal([StripItem.NextPrayer], AppSettings.Default.Strip.Items);
    }

    [Fact]
    public void Normalize_EmptyItems_DefaultsToNextPrayer() =>
        Assert.Equal([StripItem.NextPrayer], With().Normalized().Strip.Items);

    [Fact]
    public void Normalize_FiveItems_KeepsFirstThree() =>
        Assert.Equal(
            [StripItem.CpuRam, StripItem.NextPrayer, StripItem.HijriDate],
            With(StripItem.CpuRam, StripItem.NextPrayer, StripItem.HijriDate, StripItem.NowPlaying, StripItem.CpuRam).Normalized().Strip.Items);

    [Fact]
    public void Normalize_Duplicates_Removed() =>
        Assert.Equal([StripItem.NextPrayer, StripItem.CpuRam],
            With(StripItem.NextPrayer, StripItem.NextPrayer, StripItem.CpuRam).Normalized().Strip.Items);

    [Fact]
    public void Normalize_NullStrip_Defaults() =>
        Assert.True((AppSettings.Default with { Strip = null! }).Normalized().Strip.Enabled);
}

public class StripSettingsEqualityTests
{
    [Fact]
    public void SameAs_EqualContentDifferentLists_True()
    {
        var a = new StripSettings { Items = [StripItem.NextPrayer, StripItem.CpuRam] };
        var b = new StripSettings { Items = new List<StripItem> { StripItem.NextPrayer, StripItem.CpuRam } };
        Assert.True(a.SameAs(b));
    }

    [Fact]
    public void SameAs_DifferentOrder_False() =>
        Assert.False(new StripSettings { Items = [StripItem.NextPrayer, StripItem.CpuRam] }
            .SameAs(new StripSettings { Items = [StripItem.CpuRam, StripItem.NextPrayer] }));

    [Fact]
    public void SameAs_DifferentOffset_False() =>
        Assert.False(new StripSettings { OffsetFromRightPx = 10 }.SameAs(new StripSettings { OffsetFromRightPx = 20 }));
}
