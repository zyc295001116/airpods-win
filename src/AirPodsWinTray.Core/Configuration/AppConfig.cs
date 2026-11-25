using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirPodsWinTray.Core.Configuration;

public sealed class AppConfig
{
    public string? BoundContainerName { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool ShowCaseOpenPopup { get; set; } = true;
    public bool PauseOnEarRemoval { get; set; } = true;
    public bool AutoConnectOnMediaPlay { get; set; } = true;
    public bool AutoDisconnectOnLock { get; set; } = true;
    public bool AutoDisconnectOnIdle { get; set; } = true;
    public int IdleDisconnectMinutes { get; set; } = 10;
    public bool MusicModeDisableHfpHintShown { get; set; }
    public string ConnectHotkey { get; set; } = "Ctrl+Alt+P";
    public string DisconnectHotkey { get; set; } = "Ctrl+Alt+Shift+P";
    public string ReleaseHotkey { get; set; } = "Ctrl+Alt+Shift+R";

    [JsonIgnore]
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirPodsWinTray");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}