using MinecraftLauncherPerso.Services.Auth;

namespace MinecraftLauncherPerso.Services.Launch;

public interface IGameLauncher
{
    /// <summary>Lance Minecraft/Forge avec le classpath Forge, les mods installés, la session
    /// active et la RAM configurée par l'utilisateur.</summary>
    Task LaunchAsync(
        string javaExecutablePath,
        string forgeVersionId,
        string gameDirectory,
        MinecraftSession session,
        int minRamMb,
        int maxRamMb,
        CancellationToken cancellationToken = default);
}
