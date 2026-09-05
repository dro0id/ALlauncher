using System.IO;
using System.Text.Json;
using MinecraftLauncherPerso.Models;

namespace MinecraftLauncherPerso.Services.Configuration;

/// <summary>Charge/sauvegarde les préférences utilisateur (RAM, dossier de jeu, URL du VPS, ...)
/// dans %AppData%/MinecraftLauncherPerso/settings.json.</summary>
public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;

    public SettingsManager(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MinecraftLauncherPerso", "settings.json");
    }

    public LauncherSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new LauncherSettings();
        }

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
    }

    public void Save(LauncherSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
