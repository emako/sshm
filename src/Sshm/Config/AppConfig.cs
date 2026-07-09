using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sshm.Config;

public sealed class KeyBindings
{
    [JsonPropertyName("quit_keys")]
    public List<string> QuitKeys { get; set; } = [];

    [JsonPropertyName("disable_esc_quit")]
    public bool DisableEscQuit { get; set; }

    public bool ShouldQuitOnKey(string key)
    {
        if (key == "esc")
        {
            return !DisableEscQuit;
        }

        foreach (string quitKey in QuitKeys)
        {
            if (quitKey == key)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class AppConfig
{
    [JsonPropertyName("check_for_updates")]
    public bool? CheckForUpdates { get; set; }

    [JsonPropertyName("key_bindings")]
    public KeyBindings KeyBindings { get; set; } = new();

    public bool IsUpdateCheckEnabled()
    {
        return CheckForUpdates ?? true;
    }
}

public static class AppConfigService
{
    public static KeyBindings GetDefaultKeyBindings()
    {
        return new KeyBindings
        {
            QuitKeys = ["q", "ctrl+c"],
            DisableEscQuit = false,
        };
    }

    public static AppConfig GetDefaultAppConfig()
    {
        return new AppConfig
        {
            KeyBindings = GetDefaultKeyBindings(),
        };
    }

    public static AppConfig LoadAppConfig()
    {
        string configPath = PlatformPaths.GetAppConfigPath();

        if (!File.Exists(configPath))
        {
            AppConfig defaultConfig = GetDefaultAppConfig();
            string configDir = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(configDir);

            try
            {
                SaveAppConfig(defaultConfig);
            }
            catch
            {
                return defaultConfig;
            }

            return defaultConfig;
        }

        string data = File.ReadAllText(configPath);
        AppConfig? config = JsonSerializer.Deserialize(data, SshmJsonContext.Default.AppConfig);
        if (config == null)
        {
            throw new JsonException("Failed to deserialize application configuration.");
        }

        return MergeWithDefaults(config);
    }

    public static void SaveAppConfig(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        string configPath = PlatformPaths.GetAppConfigPath();
        string configDir = Path.GetDirectoryName(configPath)!;
        Directory.CreateDirectory(configDir);

        string data = JsonSerializer.Serialize(config, SshmJsonContext.Default.AppConfig);
        File.WriteAllText(configPath, data);
    }

    private static AppConfig MergeWithDefaults(AppConfig config)
    {
        AppConfig defaults = GetDefaultAppConfig();

        if (config.KeyBindings.QuitKeys.Count == 0)
        {
            config.KeyBindings.QuitKeys = [.. defaults.KeyBindings.QuitKeys];
        }

        return config;
    }
}
