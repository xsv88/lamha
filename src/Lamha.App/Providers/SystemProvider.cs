using System.Diagnostics;
using System.Runtime.InteropServices;
using Lamha.App.Interop;
using Lamha.Core.SystemInfo;

namespace Lamha.App.Providers;

public sealed record AppUsage(string Name, int ProcessCount, double PrivateGb);

public sealed record SystemSnapshot(
    double CpuPercent,
    double RamPercent,
    double RamUsedGb,
    double RamTotalGb,
    double? GpuPercent,
    IReadOnlyList<AppUsage> TopApps);

/// <summary>
/// Reads CPU, RAM and GPU load with Windows performance counters (no admin needed).
/// Called once a second, and only while the panel is open.
/// </summary>
public sealed class SystemProvider : IDisposable
{
    private static readonly TimeSpan GpuInstanceRefresh = TimeSpan.FromSeconds(5);

    private PerformanceCounter? _cpu;
    private List<PerformanceCounter> _gpu = [];
    private DateTime _gpuListedAt = DateTime.MinValue;
    private bool _gpuAvailable = true;
    private double? _lastGpu;

    public SystemSnapshot Sample(bool includeTopApps) => new(
        ReadCpu(), ReadRam(out double used, out double total), used, total, ReadGpu(),
        includeTopApps ? TopApps(3) : []);

    public void Dispose()
    {
        _cpu?.Dispose();
        foreach (var c in _gpu) c.Dispose();
    }

    private double ReadCpu()
    {
        try
        {
            // "% Processor Utility" matches Task Manager better than "% Processor Time" on modern CPUs.
            _cpu ??= new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
            return Math.Clamp(_cpu.NextValue(), 0, 100);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return 0;
        }
    }

    private static double ReadRam(out double usedGb, out double totalGb)
    {
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        if (!Native.GlobalMemoryStatusEx(ref m)) { usedGb = totalGb = 0; return 0; }
        const double Gb = 1024d * 1024 * 1024;
        totalGb = m.ullTotalPhys / Gb;
        usedGb = (m.ullTotalPhys - m.ullAvailPhys) / Gb;
        return m.dwMemoryLoad;
    }

    /// <summary>Sum of all 3D engine use across processes, like Task Manager's GPU column. Null if counters are missing.</summary>
    private double? ReadGpu()
    {
        if (!_gpuAvailable) return null;
        try
        {
            if (DateTime.UtcNow - _gpuListedAt > GpuInstanceRefresh)
            {
                foreach (var c in _gpu) c.Dispose();
                var category = new PerformanceCounterCategory("GPU Engine");
                _gpu = category.GetInstanceNames()
                    .Where(n => n.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .Select(n => new PerformanceCounter("GPU Engine", "Utilization Percentage", n))
                    .ToList();
                foreach (var c in _gpu) c.NextValue(); // First read of a counter is always 0.
                _gpuListedAt = DateTime.UtcNow;
                return _lastGpu; // Real values come on the next tick; keep showing the last one.
            }

            double sum = 0;
            foreach (var c in _gpu)
            {
                try { sum += c.NextValue(); }
                catch (InvalidOperationException) { /* Process ended since the list was made. */ }
            }
            return _lastGpu = Math.Clamp(sum, 0, 100);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            _gpuAvailable = false; // Old driver or counters disabled: show "—".
            return null;
        }
    }

    private static List<AppUsage> TopApps(int count)
    {
        var usage = new Dictionary<string, (int Count, long Bytes)>(StringComparer.OrdinalIgnoreCase);
        var withWindow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int mySession = Process.GetCurrentProcess().SessionId;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                // Only the user's own apps: same session, not a protected Windows process.
                if (p.SessionId != mySession || !ProcessFilter.IsClosable(p.ProcessName)) continue;
                if (p.MainWindowHandle != IntPtr.Zero) withWindow.Add(p.ProcessName);
                usage.TryGetValue(p.ProcessName, out var e);
                usage[p.ProcessName] = (e.Count + 1, e.Bytes + p.PrivateMemorySize64);
            }
            catch (Exception) { /* Access denied for some system processes: skip. */ }
            finally { p.Dispose(); }
        }
        const double Gb = 1024d * 1024 * 1024;
        // An app counts if at least one of its processes has a window (Chrome's helpers don't, its main does).
        return usage.Where(kv => withWindow.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value.Bytes).Take(count)
            .Select(kv => new AppUsage(kv.Key, kv.Value.Count, kv.Value.Bytes / Gb)).ToList();
    }
}
