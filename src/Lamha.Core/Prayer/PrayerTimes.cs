namespace Lamha.Core.Prayer;

/// <summary>A place on Earth and its offset from UTC.</summary>
public sealed record Location(double Latitude, double Longitude, TimeSpan UtcOffset);

/// <summary>How the Asr time is calculated.</summary>
public enum AsrMethod
{
    /// <summary>Shadow = object length (Shafi, Maliki, Hanbali). Default in Saudi Arabia.</summary>
    Standard,

    /// <summary>Shadow = twice the object length.</summary>
    Hanafi,
}

/// <summary>
/// The six times for one date. A value is null when the sun never reaches the
/// needed angle that day (for example Fajr in summer far north).
/// </summary>
public sealed record PrayerTimes(DateOnly Date, IReadOnlyDictionary<PrayerName, DateTimeOffset?> Times);
