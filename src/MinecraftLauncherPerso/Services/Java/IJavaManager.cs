using MinecraftLauncherPerso.Models;

namespace MinecraftLauncherPerso.Services.Java;

public interface IJavaManager
{
    /// <summary>
    /// Garantit qu'un Java 8 utilisable existe (portable déjà installé par ce launcher, ou détecté
    /// sur la machine), en téléchargeant et installant une build Temurin 8 sinon.
    /// </summary>
    /// <returns>Chemin complet vers l'exécutable java (java.exe sous Windows) à utiliser pour lancer Forge/Minecraft.</returns>
    Task<string> EnsureJava8Async(IProgress<JavaSetupProgress>? progress = null, CancellationToken cancellationToken = default);
}
