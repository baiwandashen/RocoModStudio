using System.Text.Json;
using RocoModStudio.Models;

namespace RocoModStudio.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var portableConfig = Path.Combine(AppContext.BaseDirectory, "Config");
        var baseDirectory = Directory.Exists(portableConfig) || IsDirectoryWritable(AppContext.BaseDirectory)
            ? AppContext.BaseDirectory
            : Path.GetTempPath();
        _settingsPath = Path.Combine(baseDirectory, "Config", "roco-mod-studio.json");
    }

    public StudioSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new StudioSettings();
            return JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(_settingsPath), JsonOptions)
                   ?? new StudioSettings();
        }
        catch
        {
            return new StudioSettings();
        }
    }

    public void Save(StudioSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
