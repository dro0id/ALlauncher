using CmlLib.Core;
using CmlLib.Core.ProcessBuilder;
using MinecraftLauncherPerso.Services.Auth;

namespace MinecraftLauncherPerso.Services.Launch;

public interface IGameLauncher
{
    /// <summary>
    /// Construit le process Forge/Minecraft (classpath, mods, JVM args) avec la session active,
    /// la RAM configurée et le Java fourni, puis le démarre. Ne bloque pas jusqu'à la fermeture du
    /// jeu : le process continue de tourner indépendamment une fois lancé.
    /// </summary>
    Task<ProcessWrapper> LaunchAsync(
        MinecraftLauncher launcher,
        string versionId,
        MinecraftSession session,
        string javaExecutablePath,
        int minRamMb,
        int maxRamMb,
        IProgress<string>? gameOutput = null,
        CancellationToken cancellationToken = default);
}
