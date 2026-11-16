namespace Lamha.Core.Prayer;

/// <summary>Which color state the prayer card is in.</summary>
public enum PrayerPhase
{
    /// <summary>Waiting for the next adhan (blue).</summary>
    Normal,

    /// <summary>Adhan has passed; waiting for iqama (yellow).</summary>
    IqamaWait,

    /// <summary>Final 60 seconds before iqama (red).</summary>
    LastMinute,
}

/// <summary>Suggested tray icon color. The App maps this to its own icon type.</summary>
public enum TrayAccentHint { None, Blue, Yellow, Red }

/// <summary>
/// The card's current state. In Normal, Target is the next adhan.
/// In IqamaWait/LastMinute, Target is the current prayer and TargetTime is its iqama.
/// </summary>
public sealed record PrayerState(PrayerPhase Phase, PrayerName Target, DateTimeOffset TargetTime, TimeSpan Remaining);
