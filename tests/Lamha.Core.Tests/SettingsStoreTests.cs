using Lamha.Core.Prayer;
using Lamha.Core.Settings;

namespace Lamha.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lamha-tests").FullName;
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(AppSettings.Default.Hotkey, s.Hotkey);
        Assert.True(s.DoNotDisturbFullscreen);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var changed = AppSettings.Default with
        {
            Hotkey = "Ctrl+Shift+L",
            PanelOpacity = 0.8,
            HiddenSections = [SectionId.NowPlaying],
            Prayer = AppSettings.Default.Prayer with
            {
                Asr = AsrMethod.Hanafi,
                OffsetMinutes = new Dictionary<PrayerName, int> { [PrayerName.Dhuhr] = 3 },
            },
        };
        store.Save(changed);
        var loaded = store.Load();

        Assert.Equal("Ctrl+Shift+L", loaded.Hotkey);
        Assert.Equal(0.8, loaded.PanelOpacity);
        Assert.Equal([SectionId.NowPlaying], loaded.HiddenSections);
        Assert.Equal(AsrMethod.Hanafi, loaded.Prayer.Asr);
        Assert.Equal(3, loaded.Prayer.OffsetMinutes[PrayerName.Dhuhr]);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsAndRenamesToBad()
    {
        File.WriteAllText(FilePath, "{ this is not json");
        var s = new SettingsStore(FilePath).Load();

        Assert.Equal(AppSettings.Default.Hotkey, s.Hotkey);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.bad.json")));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(FilePath, "");
        Assert.Equal(AppSettings.Default.Hotkey, new SettingsStore(FilePath).Load().Hotkey);
    }

    [Fact]
    public void Load_UnknownFields_Ignored()
    {
        File.WriteAllText(FilePath, """{ "Hotkey": "Ctrl+Alt+K", "SomethingFromTheFuture": 42 }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal("Ctrl+Alt+K", s.Hotkey);
        Assert.Equal(AppSettings.Default.Prayer.AlertMinutesBefore, s.Prayer.AlertMinutesBefore);
    }

    [Fact]
    public void Load_OpacityOutOfRange_Clamped()
    {
        File.WriteAllText(FilePath, """{ "PanelOpacity": 0.1 }""");
        Assert.Equal(0.6, new SettingsStore(FilePath).Load().PanelOpacity);
    }

    [Fact]
    public void Defaults_IqamaMatchSpec()
    {
        var iq = AppSettings.Default.Prayer.IqamaMinutes;
        Assert.Equal(25, iq[PrayerName.Fajr]);
        Assert.Equal(20, iq[PrayerName.Dhuhr]);
        Assert.Equal(20, iq[PrayerName.Asr]);
        Assert.Equal(10, iq[PrayerName.Maghrib]);
        Assert.Equal(20, iq[PrayerName.Isha]);
    }
}
