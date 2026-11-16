namespace Lamha.Core.SystemInfo;

/// <summary>
/// Decides which processes the "close app" button may offer. Windows parts and Lamha itself
/// are never offered, even if they use a lot of memory.
/// </summary>
public static class ProcessFilter
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "dwm", "csrss", "explorer", "Memory Compression", "System", "Registry", "smss",
        "wininit", "winlogon", "services", "lsass", "fontdrvhost", "sihost", "ctfmon", "taskhostw",
        "RuntimeBroker", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "TextInputHost",
        "MsMpEng", "audiodg", "spoolsv", "conhost", "dllhost", "Lamha",
    };

    public static bool IsClosable(string processName) => !Protected.Contains(processName);
}
