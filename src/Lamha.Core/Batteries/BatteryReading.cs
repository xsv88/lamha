namespace Lamha.Core.Batteries;

public enum DeviceKind { Headset, Mouse, Keyboard, Controller, ThisPc, Other }

/// <summary>One device's battery level at one moment.</summary>
public sealed record BatteryReading(string Name, DeviceKind Kind, int Percent, bool Charging = false)
{
    public const int LowPercent = 20;
    public bool IsLow => Percent < LowPercent && !Charging;
}
