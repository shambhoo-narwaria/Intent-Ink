using System.IO;
using System.Text.Json;

namespace IntentInk.Desktop.Infrastructure;

/// <summary>
/// App-wide user preferences.
/// Stored at %LOCALAPPDATA%\IntentInk\settings.json.
/// Never stores raw editor text.
/// </summary>
public sealed class OllamaSettings
{
    public string BaseUrl   { get; set; } = "http://localhost:11434/";
    public string ModelName { get; set; } = "";
    public int    TextLimit     { get; set; } = 1500;
    public bool   RunAtSignIn   { get; set; } = false;
    public bool   IsPaused      { get; set; } = false;
    public List<string> ExcludedProcessNames { get; set; } = [];
}

/// <summary>
/// Reads and writes <see cref="OllamaSettings"/> to disk.
/// Thread-safe for concurrent reads; serialize writes externally.
/// </summary>
public sealed class SettingsStore
{
    private static readonly string SettingsDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IntentInk");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private OllamaSettings _current = new();

    public OllamaSettings Current => _current;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var json = File.ReadAllText(SettingsFile);
            _current = JsonSerializer.Deserialize<OllamaSettings>(json, JsonOpts)
                       ?? new OllamaSettings();
        }
        catch
        {
            _current = new OllamaSettings(); // safe default on corrupt file
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(_current, JsonOpts);
        File.WriteAllText(SettingsFile, json);
    }

    public void Update(Action<OllamaSettings> mutate)
    {
        mutate(_current);
        Save();
    }
}
