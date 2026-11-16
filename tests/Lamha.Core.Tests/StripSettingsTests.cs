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
