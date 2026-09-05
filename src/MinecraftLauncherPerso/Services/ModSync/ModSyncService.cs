namespace MinecraftLauncherPerso.Services.ModSync;

/// <summary>
/// TODO (prochaine étape) : télécharger un manifeste JSON depuis modsServerBaseUrl (liste de fichiers
/// + hash SHA-256), le comparer à un manifeste local mis en cache, puis ne télécharger/extraire que
/// les fichiers manquants ou modifiés (mods.zip / config.zip hébergés sur le VPS).
/// </summary>
public sealed class ModSyncService : IModSyncService
{
    public Task SyncAsync(
        string modsServerBaseUrl,
        string gameDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Synchronisation mods/config à implémenter (voir README, étape suivante).");
    }
}
