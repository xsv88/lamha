namespace Lamha.Core.Batteries;

/// <summary>Cleans and orders readings coming from several Windows sources.</summary>
public static class BatteryRules
{
    /// <summary>
    /// Clamps to 0–100, keeps one reading per device name (the lowest, the safer number when two
    /// sources disagree), and sorts lowest first with this PC's own battery last.
    /// </summary>
    public static IReadOnlyList<BatteryReading> Merge(IEnumerable<BatteryReading> readings) =>
        readings
            .Select(r => r with { Percent = Math.Clamp(r.Percent, 0, 100) })
            .GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MinBy(r => r.Percent)!)
            .OrderBy(r => r.Kind == DeviceKind.ThisPc)
            .ThenBy(r => r.Percent)
            .ToList();

    /// <summary>The device most in need of charging (charging devices excluded).</summary>
    public static BatteryReading? Lowest(IEnumerable<BatteryReading> readings) =>
        readings.Where(r => !r.Charging).MinBy(r => r.Percent);
}

/// <summary>
/// One alert when a device drops under 20 %. The alert is re-armed only after the device is
/// seen at 30 % or more, so a device bouncing around 19–21 % or reconnecting does not spam.
/// </summary>
public sealed class LowBatteryAlerts
{
    private const int ReArmPercent = 30;
    private readonly HashSet<string> _alerted = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BatteryReading> Tick(IReadOnlyList<BatteryReading> now)
    {
        var fresh = new List<BatteryReading>();
        foreach (var r in now)
        {
            if (r.Percent >= ReArmPercent) _alerted.Remove(r.Name);
            else if (r.IsLow && _alerted.Add(r.Name)) fresh.Add(r);
        }
        return fresh;
    }
}
