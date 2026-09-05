namespace MinecraftLauncherPerso.Services.Forge;

public interface IForgeManager
{
    /// <summary>
    /// Garantit qu'un profil Forge (ex. 1.16.5-36.2.34) est installé dans le dossier .minecraft,
    /// en l'installant si nécessaire (via CmlLib.Core si supporté, sinon en exécutant l'installeur
    /// officiel Forge en mode silencieux).
    /// </summary>
    /// <returns>Identifiant de version à passer au lanceur (ex. "1.16.5-forge-36.2.34").</returns>
    Task<string> EnsureForgeInstalledAsync(
        string minecraftVersion,
        string forgeVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
