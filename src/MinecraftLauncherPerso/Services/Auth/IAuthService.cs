namespace MinecraftLauncherPerso.Services.Auth;

public sealed record MinecraftSession(string Username, string Uuid, string AccessToken);

public interface IAuthService
{
    /// <summary>
    /// Récupère la session du compte déjà connecté sur le launcher officiel Minecraft installé
    /// sur la machine (lecture de launcher_accounts.json dans %AppData%/.minecraft), sans
    /// implémenter de flux OAuth Microsoft complet.
    /// </summary>
    MinecraftSession GetActiveSession();
}
