using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using MinecraftLauncherPerso.Services.Auth;

namespace MinecraftLauncherPerso.Services.Launch;

public sealed class GameLauncher : IGameLauncher
{
    public async Task<ProcessWrapper> LaunchAsync(
        MinecraftLauncher launcher,
        string versionId,
        MinecraftSession session,
        string javaExecutablePath,
        int minRamMb,
        int maxRamMb,
        IProgress<string>? gameOutput = null,
        CancellationToken cancellationToken = default)
    {
        var mSession = new MSession(session.Username, session.AccessToken, session.Uuid);

        var process = await launcher.BuildProcessAsync(versionId, new MLaunchOption
        {
            Session = mSession,
            JavaPath = javaExecutablePath,
            MinimumRamMb = minRamMb,
            MaximumRamMb = maxRamMb,
        });

        var processWrapper = new ProcessWrapper(process);
        processWrapper.OutputReceived += (_, line) => gameOutput?.Report(line);
        processWrapper.StartWithEvents();

        return processWrapper;
    }
}
