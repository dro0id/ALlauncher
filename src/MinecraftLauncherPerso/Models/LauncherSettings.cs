using System.IO;

namespace MinecraftLauncherPerso.Models;

/// <summary>Préférences utilisateur persistées entre deux lancements (settings.json).</summary>
public sealed class LauncherSettings
{
    public string MinecraftVersion { get; set; } = "1.16.5";

    public string ForgeVersion { get; set; } = "36.2.34";

    /// <summary>URL de base du VPS hébergeant manifest.json + les fichiers mods/config (ex. https://vps.example.com/pack).</summary>
    public string ModsServerBaseUrl { get; set; } = "";

    public int MinRamMb { get; set; } = 2048;

    public int MaxRamMb { get; set; } = 6144;

    /// <summary>
    /// Dossier .minecraft utilisé par CE launcher pour le pack modé — volontairement distinct du
    /// .minecraft du launcher officiel (qui, lui, n'est utilisé que pour lire la session active).
    /// </summary>
    public string GameDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MinecraftLauncherPerso", "game");
}
