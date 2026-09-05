namespace MinecraftLauncherPerso.Services.Forge;

/// <summary>
/// TODO (prochaine étape) : installer Forge 1.16.5-36.2.34 via CmlLib.Core.Installer.Forge,
/// avec repli sur le téléchargement + exécution silencieuse de l'installeur officiel
/// (forge-1.16.5-36.2.34-installer.jar --installClient) si la version n'est pas supportée
/// nativement par CmlLib.Core.
/// </summary>
public sealed class ForgeManager : IForgeManager
{
    public Task<string> EnsureForgeInstalledAsync(
        string minecraftVersion,
        string forgeVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Installation Forge à implémenter (voir README, étape suivante).");
    }
}
