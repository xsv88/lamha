using System.Diagnostics;
using System.Runtime;
using System.Windows.Threading;
using Lamha.App.Interop;

namespace Lamha.App.Services;

/// <summary>
/// After the panel closes, frees memory the app no longer needs: a full garbage collection
/// (compacting the large-object heap, where album covers live) and then gives unused pages back
/// to Windows. Lamha mostly sits idle, so this keeps it small between uses.
/// </summary>
public sealed class MemoryTrimmer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    public MemoryTrimmer() => _timer.Tick += (_, _) => { _timer.Stop(); Trim(); };

    /// <summary>Call when the panel closes. Waits 5 s so a quick reopen stays instant.</summary>
    public void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Cancel() => _timer.Stop();

    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            using var me = Process.GetCurrentProcess();
            Native.EmptyWorkingSet(me.Handle);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }
}
