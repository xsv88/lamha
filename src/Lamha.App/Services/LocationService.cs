using Lamha.Core.Prayer;
using Lamha.Core.Settings;
using Windows.Devices.Geolocation;

namespace Lamha.App.Services;

/// <summary>Finds where to calculate prayer times for: settings first, then Windows location, then Dammam.</summary>
public static class LocationService
{
    public static readonly (double Lat, double Lon) Dammam = (26.4207, 50.0888);

    public static async Task<(double Lat, double Lon)> ResolveAsync(LocationSetting setting)
    {
        if (!setting.Auto && setting.Latitude is double lat && setting.Longitude is double lon)
            return (lat, lon);

        try
        {
            if (await Geolocator.RequestAccessAsync() == GeolocationAccessStatus.Allowed)
            {
                var geo = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                var pos = await geo.GetGeopositionAsync(TimeSpan.FromHours(6), TimeSpan.FromSeconds(10));
                return (pos.Coordinate.Point.Position.Latitude, pos.Coordinate.Point.Position.Longitude);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex); // Location off or unavailable: fall back quietly.
        }
        return Dammam;
    }

    /// <summary>Location for a given date, using the PC's time zone (handles DST if the user travels).</summary>
    public static Location For((double Lat, double Lon) p, DateOnly date) =>
        new(p.Lat, p.Lon, TimeZoneInfo.Local.GetUtcOffset(date.ToDateTime(new TimeOnly(12, 0))));
}
