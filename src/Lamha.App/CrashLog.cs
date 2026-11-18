using System.IO;
using System.Windows;

namespace Lamha.App;

/// <summary>
/// Writes unhandled exceptions to %AppData%\Lamha\logs\lamha-yyyyMMdd.log
/// and keeps only the newest 5 log files.
/// </summary>
internal static class CrashLog
{
    private const int MaxFiles = 5;

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lamha", "logs");

    public static void Install(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Write(e.Exception);
            e.Handled = true; // Keep the tray icon alive after a UI error.
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Writes one diagnostic line (not an error) to the same log file.</summary>
    public static void Note(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(Path.Combine(LogDir, $"lamha-{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:O}] NOTE {message}{Environment.NewLine}");
        }
        catch { /* Logging must never break the app. */ }
    }

    public static void Write(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            Directory.CreateDirectory(LogDir);
            string file = Path.Combine(LogDir, $"lamha-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
            Prune();
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void Prune()
    {
        var old = new DirectoryInfo(LogDir)
            .GetFiles("lamha-*.log")
            .OrderByDescending(f => f.Name)
            .Skip(MaxFiles);
        foreach (var f in old) f.Delete();
    }
}
