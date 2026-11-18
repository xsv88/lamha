using Lamha.Core.Batteries;

namespace Lamha.Core.Tests;

public class BatteryRulesTests
{
    private static BatteryReading B(string name, int pct, DeviceKind kind = DeviceKind.Headset, bool charging = false) =>
        new(name, kind, pct, charging);

    [Fact]
    public void Merge_SameNameTwoSources_KeepsOne()
    {
        var list = BatteryRules.Merge([B("Buds", 40), B("buds", 38), B("Mouse", 70, DeviceKind.Mouse)]);
        Assert.Equal(2, list.Count);
        Assert.Equal(38, list.Single(b => b.Name.Equals("Buds", StringComparison.OrdinalIgnoreCase)).Percent);
    }

    [Fact]
    public void Merge_SortsLowestFirst_ThisPcLast()
    {
        var list = BatteryRules.Merge([B("Surface", 10, DeviceKind.ThisPc), B("Mouse", 70, DeviceKind.Mouse), B("Buds", 40)]);
        Assert.Equal(["Buds", "Mouse", "Surface"], list.Select(b => b.Name));
    }

    [Fact]
    public void Normalize_OutOfRange_Clamped()
    {
        var list = BatteryRules.Merge([B("A", 140), B("B", -5)]);
        Assert.Equal(100, list.Single(b => b.Name == "A").Percent);
        Assert.Equal(0, list.Single(b => b.Name == "B").Percent);
    }

    [Fact]
    public void Lowest_IgnoresCharging()
    {
        var lowest = BatteryRules.Lowest([B("Surface", 5, DeviceKind.ThisPc, charging: true), B("Buds", 40)]);
        Assert.Equal("Buds", lowest!.Name);
    }

    [Fact]
    public void Alerts_DropBelow20_AlertsOnce()
    {
        var a = new LowBatteryAlerts();
        Assert.Empty(a.Tick([B("Buds", 25)]));
        Assert.Single(a.Tick([B("Buds", 19)]));
        Assert.Empty(a.Tick([B("Buds", 15)]));
    }

    [Fact]
    public void Alerts_RechargedTo30_ReArms()
    {
        var a = new LowBatteryAlerts();
        a.Tick([B("Buds", 15)]);
        a.Tick([B("Buds", 25)]);           // Not yet re-armed.
        Assert.Empty(a.Tick([B("Buds", 18)]));
        a.Tick([B("Buds", 30)]);           // Re-armed.
        Assert.Single(a.Tick([B("Buds", 18)]));
    }

    [Fact]
    public void Alerts_DeviceGoneAndBackLow_NoSecondAlert()
    {
        var a = new LowBatteryAlerts();
        a.Tick([B("Buds", 15)]);
        a.Tick([]);
        Assert.Empty(a.Tick([B("Buds", 14)]));
    }

    [Fact]
    public void Alerts_ChargingLow_NoAlert() =>
        Assert.Empty(new LowBatteryAlerts().Tick([B("Surface", 10, DeviceKind.ThisPc, charging: true)]));
}
