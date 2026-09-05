namespace MinecraftLauncherPerso.Models;

/// <summary>
/// Résultat de la détection d'un exécutable Java : chemin, chaîne de version brute
/// (ex. "1.8.0_392" ou "17.0.9") et version majeure normalisée (8, 17, ...).
/// </summary>
public sealed record JavaVersionInfo(string ExecutablePath, string VersionString, int MajorVersion);
