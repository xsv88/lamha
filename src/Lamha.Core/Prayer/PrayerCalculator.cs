namespace Lamha.Core.Prayer;

/// <summary>
/// Calculates prayer times with the Umm al-Qura method:
/// Fajr at sun −18.5°, Isha 90 minutes after Maghrib (120 in Ramadan).
/// Sun position uses the common low-precision formulas from the
/// U.S. Naval Observatory (accurate to about one minute).
/// </summary>
public static class PrayerCalculator
{
    private const double FajrAngle = 18.5;
    private const double SunriseSetAngle = 0.833; // Refraction + sun radius.
    private const int MaxOffsetMinutes = 30;

    public static PrayerTimes Calculate(
        DateOnly date,
        Location loc,
        IReadOnlyDictionary<PrayerName, int> offsetMinutes,
        bool isRamadan,
        AsrMethod asr = AsrMethod.Standard)
    {
        double jd = JulianDate(date) - loc.Longitude / (15 * 24.0);
        double tz = loc.UtcOffset.TotalHours;

        // Each time is solved at its own approximate hour, because the sun moves during the day.
        double? fajr = SunAngleTime(jd, loc, tz, FajrAngle, guessHour: 5, beforeNoon: true);
        double? shuruq = SunAngleTime(jd, loc, tz, SunriseSetAngle, guessHour: 6, beforeNoon: true);
        double dhuhr = Noon(jd, loc, tz, guessHour: 12) + 1.0 / 60; // Umm al-Qura adds 1 minute after zenith.
        double? asrTime = AsrTime(jd, loc, tz, asr == AsrMethod.Hanafi ? 2 : 1);
        double? maghrib = SunAngleTime(jd, loc, tz, SunriseSetAngle, guessHour: 18, beforeNoon: false);
        double? isha = maghrib + (isRamadan ? 2.0 : 1.5);

        var raw = new Dictionary<PrayerName, double?>
        {
            [PrayerName.Fajr] = fajr,
            [PrayerName.Shuruq] = shuruq,
            [PrayerName.Dhuhr] = dhuhr,
            [PrayerName.Asr] = asrTime,
            [PrayerName.Maghrib] = maghrib,
            [PrayerName.Isha] = isha,
        };

        var result = new Dictionary<PrayerName, DateTimeOffset?>();
        foreach (var (name, hours) in raw)
        {
            int offset = Math.Clamp(offsetMinutes.GetValueOrDefault(name), -MaxOffsetMinutes, MaxOffsetMinutes);
            result[name] = hours is double h ? ToDateTime(date, h, loc.UtcOffset).AddMinutes(offset) : null;
        }
        return new PrayerTimes(date, result);
    }

    // ---- Sun position -------------------------------------------------------

    private static double JulianDate(DateOnly d)
    {
        int y = d.Year, m = d.Month;
        if (m <= 2) { y -= 1; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d.Day + b - 1524.5;
    }

    /// <summary>Returns (declination in degrees, equation of time in hours) for a Julian date.</summary>
    private static (double Decl, double EqT) SunPosition(double jd)
    {
        double d = jd - 2451545.0;
        double g = Fix(357.529 + 0.98560028 * d, 360);
        double q = Fix(280.459 + 0.98564736 * d, 360);
        double l = Fix(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g), 360);
        double e = 23.439 - 0.00000036 * d;
        double ra = Fix(ArcTan2(Cos(e) * Sin(l), Cos(l)) / 15, 24);
        double decl = ArcSin(Sin(e) * Sin(l));
        double eqt = q / 15 - ra;
        return (decl, eqt);
    }

    /// <summary>Local clock time (hours) of solar noon.</summary>
    private static double Noon(double jd, Location loc, double tz, double guessHour)
    {
        var (_, eqt) = SunPosition(jd + guessHour / 24);
        return Fix(12 - eqt, 24) - loc.Longitude / 15 + tz;
    }

    /// <summary>Local clock time when the sun is <paramref name="angle"/> degrees below the horizon, or null.</summary>
    private static double? SunAngleTime(double jd, Location loc, double tz, double angle, double guessHour, bool beforeNoon)
    {
        var (decl, _) = SunPosition(jd + guessHour / 24);
        double noon = Noon(jd, loc, tz, guessHour);
        double cosH = (-Sin(angle) - Sin(decl) * Sin(loc.Latitude)) / (Cos(decl) * Cos(loc.Latitude));
        if (cosH is < -1 or > 1 || double.IsNaN(cosH)) return null; // Sun never reaches this angle today.
        double hours = ArcCos(cosH) / 15;
        return noon + (beforeNoon ? -hours : hours);
    }

    /// <summary>Asr: when an object's shadow = factor × its length + its noon shadow.</summary>
    private static double? AsrTime(double jd, Location loc, double tz, int factor)
    {
        var (decl, _) = SunPosition(jd + 13.0 / 24);
        double altitude = ArcCot(factor + Tan(Math.Abs(loc.Latitude - decl)));
        return SunAngleTime(jd, loc, tz, -altitude, guessHour: 13, beforeNoon: false);
    }

    private static DateTimeOffset ToDateTime(DateOnly date, double hours, TimeSpan offset)
    {
        // Round to the nearest minute, like published timetables.
        long minutes = (long)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), offset).AddMinutes(minutes);
    }

    // ---- Degree-based math helpers -----------------------------------------

    private static double Rad(double deg) => deg * Math.PI / 180;
    private static double Deg(double rad) => rad * 180 / Math.PI;
    private static double Sin(double d) => Math.Sin(Rad(d));
    private static double Cos(double d) => Math.Cos(Rad(d));
    private static double Tan(double d) => Math.Tan(Rad(d));
    private static double ArcSin(double x) => Deg(Math.Asin(x));
    private static double ArcCos(double x) => Deg(Math.Acos(x));
    private static double ArcTan2(double y, double x) => Deg(Math.Atan2(y, x));
    private static double ArcCot(double x) => Deg(Math.Atan(1 / x));
    private static double Fix(double a, double b) => a - b * Math.Floor(a / b);
}
