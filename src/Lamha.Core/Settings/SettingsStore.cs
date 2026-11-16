using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lamha.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. Never throws on a bad file:
/// a broken file is renamed to settings.bad.json and defaults are used.
/// </summary>
public sealed class SettingsStore(string filePath)
{
    // Reflection-based on purpose: the source generator resets missing init-only
    // properties to null instead of keeping their defaults.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = filePath;

    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return AppSettings.Default;

        try
        {
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return AppSettings.Default;

            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return (loaded ?? AppSettings.Default).Normalized();
        }
        catch (JsonException)
        {
            MoveAside(); // Broken content: keep it for inspection, start fresh.
            return AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default; // Locked (OneDrive, antivirus): the file itself may be fine, leave it.
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Normalized(), Options));
        File.Move(temp, FilePath, overwrite: true); // Replace in one step so a crash never leaves half a file.
    }

    private void MoveAside()
    {
        try
        {
            string bad = Path.Combine(Path.GetDirectoryName(FilePath)!, "settings.bad.json");
            File.Move(FilePath, bad, overwrite: true);
        }
        catch (IOException)
        {
            // If we cannot rename it, defaults still load; next Save overwrites it.
        }
    }
}
