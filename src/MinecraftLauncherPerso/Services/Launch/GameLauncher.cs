using MinecraftLauncherPerso.Services.Auth;

namespace MinecraftLauncherPerso.Services.Launch;

/// <summary>
/// TODO (prochaine étape) : construire un MinecraftLauncher (CmlLib.Core) pointant sur gameDirectory,
/// avec MSession.CreateOfflineSession/CreateSession selon la session récupérée, MLaunchOption pour
/// MinimumRamMb/MaximumRamMb et JavaPath = javaExecutablePath, puis lancer le process forgeVersionId.
/// </summary>
public sealed class GameLauncher : IGameLauncher
{
    public Task LaunchAsync(
        string javaExecutablePath,
        string forgeVersionId,
        string gameDirectory,
        MinecraftSession session,
        int minRamMb,
        int maxRamMb,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Lancement du jeu à implémenter (voir README, étape suivante).");
    }
}
