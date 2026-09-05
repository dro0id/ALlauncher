using CmlLib.Core;

namespace MinecraftLauncherPerso.Services.Forge;

public interface IForgeManager
{
    /// <summary>
    /// Garantit qu'un profil Forge (ex. Minecraft 1.16.5 + Forge 36.2.34) est installé dans le
    /// dossier de jeu de <paramref name="launcher"/>, en l'installant si nécessaire.
    /// </summary>
    /// <returns>Identifiant de version à passer à <c>MinecraftLauncher.BuildProcessAsync</c> (ex. "1.16.5-forge-36.2.34").</returns>
    Task<string> EnsureForgeInstalledAsync(
        MinecraftLauncher launcher,
        string minecraftVersion,
        string forgeVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
