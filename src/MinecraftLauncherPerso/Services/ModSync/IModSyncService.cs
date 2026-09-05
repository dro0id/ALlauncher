namespace MinecraftLauncherPerso.Services.ModSync;

public interface IModSyncService
{
    /// <summary>
    /// Synchronise les dossiers mods/ et config/ depuis le VPS : ne télécharge que si un manifeste
    /// distant (hash/version) diffère de l'état local, pour éviter de re-télécharger à chaque lancement.
    /// </summary>
    Task SyncAsync(string modsServerBaseUrl, string gameDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
