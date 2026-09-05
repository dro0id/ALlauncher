using System.Text.Json.Serialization;

namespace MinecraftLauncherPerso.Services.ModSync;

/// <summary>Une entrée du manifeste : chemin relatif au dossier de jeu, hash SHA-256 attendu, et
/// chemin relatif à <c>ModsServerBaseUrl</c> pour le télécharger (souvent identique à Path).</summary>
public sealed class ModFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

/// <summary>
/// Manifeste JSON attendu à <c>{ModsServerBaseUrl}/manifest.json</c> sur le VPS, listant tous les
/// fichiers de mods/ et config/ à synchroniser localement, par exemple :
/// <code>
/// {
///   "version": "2026-01-01",
///   "files": [
///     { "path": "mods/create-1.16.5-0.3.2g.jar", "sha256": "...", "url": "mods/create-1.16.5-0.3.2g.jar" },
///     { "path": "config/create/common.toml", "sha256": "...", "url": "config/create/common.toml" }
///   ]
/// }
/// </code>
/// </summary>
public sealed class ModManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("files")]
    public List<ModFileEntry> Files { get; set; } = new();
}
