using Lamha.Core.Batteries;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;
using Windows.System.Power;

namespace Lamha.App.Providers;

/// <summary>
/// Collects battery levels from every source Windows offers to normal apps:
/// the Bluetooth battery value shown in Settings, BLE Battery Service, game controllers,
/// and this PC's own battery. Each source can fail on its own without hiding the others.
/// </summary>
public sealed class BatteryProvider
{
    /// <summary>DEVPKEY_Bluetooth_Battery: the percentage Windows shows in Settings → Bluetooth.</summary>
    private const string BluetoothBatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string ContainerIdKey = "System.Devices.ContainerId";
    private const string ConnectedKey = "System.Devices.Connected";
    private static readonly TimeSpan GattTimeout = TimeSpan.FromSeconds(3);

    // Asking only for devices that have a battery value is much cheaper than listing every device.
    private const string WithBatteryQuery = "System.Devices.Present:=System.StructuredQueryType.Boolean#True AND " + BluetoothBatteryKey + ":<>[]";
    private const string AllPresentQuery = "System.Devices.Present:=System.StructuredQueryType.Boolean#True";
    private static bool _filterUnsupported;

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var all = new List<BatteryReading>();
        await Collect(all, ReadBluetoothPropertyAsync);
        await Collect(all, ReadGattAsync);
        await Collect(all, () => Task.FromResult(ReadControllers()));
        await Collect(all, () => Task.FromResult(ReadThisPc()));
        return BatteryRules.Merge(all);
    }

    private static async Task Collect(List<BatteryReading> into, Func<Task<IReadOnlyList<BatteryReading>>> source)
    {
        try { into.AddRange(await source()); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

    /// <summary>Source 1: devices whose driver reports battery to Windows (classic and LE Bluetooth).</summary>
    private static async Task<IReadOnlyList<BatteryReading>> ReadBluetoothPropertyAsync()
    {
        DeviceInformationCollection devices;
        try
        {
            devices = await DeviceInformation.FindAllAsync(_filterUnsupported ? AllPresentQuery : WithBatteryQuery,
                [BluetoothBatteryKey, ContainerIdKey], DeviceInformationKind.Device);
        }
        catch (Exception ex) when (!_filterUnsupported)
        {
            // Some Windows builds reject the property filter: fall back to all present devices (slower, still correct).
            CrashLog.Write(ex);
            _filterUnsupported = true;
            devices = await DeviceInformation.FindAllAsync(AllPresentQuery, [BluetoothBatteryKey, ContainerIdKey], DeviceInformationKind.Device);
        }

        var result = new List<BatteryReading>();
        var seenContainers = new HashSet<Guid>();
        foreach (var d in devices)
        {
            if (!d.Properties.TryGetValue(BluetoothBatteryKey, out var value) || value is not byte pct) continue;

            // One physical device has several entries (audio, hands-free...): use its container's name once.
            string name = d.Name;
            if (d.Properties.TryGetValue(ContainerIdKey, out var c) && c is Guid container)
            {
                if (!seenContainers.Add(container)) continue;
                try
                {
                    var box = await DeviceInformation.CreateFromIdAsync(container.ToString("B"), [ConnectedKey], DeviceInformationKind.DeviceContainer);
                    // Paired devices stay "present" after they are switched off and may keep the last value: skip them.
                    if (box.Properties.TryGetValue(ConnectedKey, out var connected) && connected is false) continue;
                    if (!string.IsNullOrWhiteSpace(box.Name)) name = box.Name;
                }
                catch (Exception ex) { CrashLog.Write(ex); }
            }
            result.Add(new BatteryReading(name, KindFromName(name), pct));
        }
        return result;
    }

    /// <summary>Source 2: connected Bluetooth LE devices with the standard Battery Service.</summary>
    private static async Task<IReadOnlyList<BatteryReading>> ReadGattAsync()
    {
        var selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
        var result = new List<BatteryReading>();
        foreach (var info in await DeviceInformation.FindAllAsync(selector))
        {
            try
            {
                // A sleeping device can stall a radio read for many seconds: give each device 3 s at most.
                if (await ReadOneGattAsync(info.Id).WaitAsync(GattTimeout) is { } reading) result.Add(reading);
            }
            catch (TimeoutException) { /* Asleep or out of range: try again next time. */ }
            catch (Exception ex)
            {
                // Mice and keyboards often deny direct access (Windows owns their HID service): skip quietly.
                if (ex is not UnauthorizedAccessException) CrashLog.Write(ex);
            }
        }
        return result;
    }

    private static async Task<BatteryReading?> ReadOneGattAsync(string id)
    {
        using var device = await BluetoothLEDevice.FromIdAsync(id);
        if (device is null) return null;
        var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached);
        try
        {
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0) return null;
            var chars = await services.Services[0].GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Cached);
            if (chars.Status != GattCommunicationStatus.Success || chars.Characteristics.Count == 0) return null;

            var read = await chars.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status != GattCommunicationStatus.Success || read.Value.Length < 1) return null;

            var bytes = new byte[1];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(read.Value)) reader.ReadBytes(bytes);
            return new BatteryReading(device.Name, KindFromName(device.Name), bytes[0]);
        }
        finally
        {
            foreach (var service in services.Services) service.Dispose();
        }
    }

    /// <summary>Source 3: game controllers (Xbox, etc.).</summary>
    private static IReadOnlyList<BatteryReading> ReadControllers()
    {
        var result = new List<BatteryReading>();
        foreach (var pad in RawGameController.RawGameControllers)
        {
            var report = pad.TryGetBatteryReport();
            if (report?.RemainingCapacityInMilliwattHours is int left && report.FullChargeCapacityInMilliwattHours is int full && full > 0)
            {
                bool charging = report.Status == Windows.System.Power.BatteryStatus.Charging;
                result.Add(new BatteryReading(pad.DisplayName, DeviceKind.Controller, left * 100 / full, charging));
            }
        }
        return result;
    }

    /// <summary>Source 4: this PC's own battery (laptops and tablets only).</summary>
    private static IReadOnlyList<BatteryReading> ReadThisPc()
    {
        if (PowerManager.BatteryStatus == BatteryStatus.NotPresent) return [];
        // A weak charger ("Inadequate") still drains the battery, so it does not count as charging.
        bool charging = PowerManager.BatteryStatus == BatteryStatus.Charging
                        || (PowerManager.BatteryStatus == BatteryStatus.Idle && PowerManager.PowerSupplyStatus == PowerSupplyStatus.Adequate);
        return [new BatteryReading("This PC", DeviceKind.ThisPc, PowerManager.RemainingChargePercent, charging)];
    }

    /// <summary>Best guess from the name; only used for the icon.</summary>
    private static DeviceKind KindFromName(string name)
    {
        string n = name.ToLowerInvariant();
        string[] headset = ["bud", "pod", "head", "ear", "wh-", "wf-", "jabra", "pudding", "sound", "audio"];
        if (headset.Any(n.Contains)) return DeviceKind.Headset;
        if (n.Contains("mouse") || n.Contains("mx ")) return DeviceKind.Mouse;
        if (n.Contains("keyboard") || n.Contains("keys")) return DeviceKind.Keyboard;
        if (n.Contains("controller") || n.Contains("gamepad")) return DeviceKind.Controller;
        return DeviceKind.Other;
    }
}
