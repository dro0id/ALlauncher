using System.IO;
using System.Text;

namespace MinecraftLauncherPerso.Models;

/// <summary>Préférences utilisateur persistées entre deux lancements (settings.json).</summary>
public sealed class LauncherSettings
{
    public string MinecraftVersion { get; set; } = "1.16.5";

    public string ForgeVersion { get; set; } = "36.2.34";

    /// <summary>
    /// URL directe de l'archive .zip du modpack (mods/ + config/ à sa racine) hébergée sur le VPS.
    /// Valeur par défaut encodée en base64 (voir <see cref="DecodeDefaultModpackUrl"/>) : le dépôt
    /// étant public, ça évite que l'IP du VPS apparaisse en clair dans le code source ou soit
    /// indexée telle quelle par un moteur de recherche/scanner, sans empêcher le launcher de
    /// fonctionner "out of the box". Peut être écrasée dans settings.json si le VPS change.
    /// </summary>
    public string ModpackZipUrl { get; set; } = DecodeDefaultModpackUrl();

    private static string DecodeDefaultModpackUrl() =>
        Encoding.UTF8.GetString(Convert.FromBase64String(
            "aHR0cDovLzE4NS4xODUuODIuMTgwL21vZHBhY2svQWxnYXJvbi1tb2RkZWQuemlw"));

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
