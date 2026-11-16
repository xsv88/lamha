using Lamha.Core.Prayer;

namespace Lamha.Core.Settings;

/// <summary>Panel sections. Phase 1 has views for Header, Prayer, NowPlaying and PcLoad.</summary>
public enum SectionId { Header, Prayer, NowPlaying, Events, WeatherNetwork, Batteries, PcLoad }

public enum ThemeMode { FollowWindows, Light, Dark }

public enum LanguageMode { Auto, Arabic, English }

/// <summary>Where prayer times are calculated for. Auto = Windows location.</summary>
public sealed record LocationSetting(bool Auto, string? CityName, double? Latitude, double? Longitude)
{
    public static LocationSetting Default { get; } = new(Auto: true, CityName: null, Latitude: null, Longitude: null);
}

public sealed record PrayerSettings
{
    public string Method { get; init; } = "UmmAlQura";
    public AsrMethod Asr { get; init; } = AsrMethod.Standard;
    public bool AlertsEnabled { get; init; } = true;
    public int AlertMinutesBefore { get; init; } = 10;

    /// <summary>Per-prayer shift in minutes, −30…+30. Missing = 0.</summary>
    public IReadOnlyDictionary<PrayerName, int> OffsetMinutes { get; init; } = new Dictionary<PrayerName, int>();

    /// <summary>Minutes from adhan to iqama per prayer.</summary>
    public IReadOnlyDictionary<PrayerName, int> IqamaMinutes { get; init; } = new Dictionary<PrayerName, int>
    {
        [PrayerName.Fajr] = 25,
        [PrayerName.Dhuhr] = 20,
        [PrayerName.Asr] = 20,
        [PrayerName.Maghrib] = 10,
        [PrayerName.Isha] = 20,
    };
}

/// <summary>Everything the user can change. Saved as JSON in %AppData%\Lamha\settings.json.</summary>
public sealed record AppSettings
{
    public const double MinOpacity = 0.6;
    public const double MaxOpacity = 1.0;

    public static AppSettings Default { get; } = new();

    public int Version { get; init; } = 1;

    public IReadOnlyList<SectionId> SectionOrder { get; init; } =
        [SectionId.Header, SectionId.Prayer, SectionId.NowPlaying, SectionId.Events,
         SectionId.WeatherNetwork, SectionId.Batteries, SectionId.PcLoad];

    public IReadOnlyList<SectionId> HiddenSections { get; init; } = [];
    public LocationSetting Location { get; init; } = LocationSetting.Default;
    public PrayerSettings Prayer { get; init; } = new();
    public string Hotkey { get; init; } = "Ctrl+Alt+N";
    public bool DoNotDisturbFullscreen { get; init; } = true;
    public bool StartWithWindows { get; init; } = true;
    public ThemeMode Theme { get; init; } = ThemeMode.FollowWindows;
    public double PanelOpacity { get; init; } = 1.0;
    public LanguageMode Language { get; init; } = LanguageMode.Auto;

    /// <summary>Returns a copy with every value inside its allowed range.</summary>
    public AppSettings Normalized() => this with
    {
        PanelOpacity = Math.Clamp(PanelOpacity, MinOpacity, MaxOpacity),
        Prayer = Prayer with
        {
            AlertMinutesBefore = Math.Clamp(Prayer.AlertMinutesBefore, 0, 120),
            OffsetMinutes = Prayer.OffsetMinutes.ToDictionary(kv => kv.Key, kv => Math.Clamp(kv.Value, -30, 30)),
            IqamaMinutes = Prayer.IqamaMinutes.ToDictionary(kv => kv.Key, kv => Math.Clamp(kv.Value, 0, 60)),
        },
    };
}
