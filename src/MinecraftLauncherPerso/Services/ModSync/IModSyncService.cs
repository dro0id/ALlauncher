namespace MinecraftLauncherPerso.Services.ModSync;

public interface IModSyncService
{
    /// <summary>
    /// Synchronise le pack de mods/config depuis une archive .zip unique hébergée sur le VPS
    /// (ex. http://vps/modpack/Algaron-modded.zip). Ne la retélécharge que si elle a changé
    /// côté serveur depuis la dernière synchro, pas à chaque lancement.
    /// </summary>
    Task SyncAsync(string modpackZipUrl, string gameDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
