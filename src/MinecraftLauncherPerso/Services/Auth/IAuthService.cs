namespace MinecraftLauncherPerso.Services.Auth;

public sealed record MinecraftSession(string Username, string Uuid, string AccessToken);

/// <summary>Instructions du device code flow à mettre bien en évidence dans l'UI (pas juste dans un journal qui défile).</summary>
public sealed record DeviceCodeInfo(string Message, string VerificationUrl, string UserCode);

public interface IAuthService
{
    /// <summary>
    /// Authentifie l'utilisateur via OAuth Microsoft (device code flow) puis la chaîne
    /// Xbox Live -> XSTS -> Minecraft, et retourne la session à utiliser pour lancer le jeu.
    /// Réutilise silencieusement une session Microsoft précédemment mise en cache tant qu'elle
    /// est valide ; ne redemande une connexion interactive (device code) que si elle a expiré ou
    /// n'existe pas encore.
    /// </summary>
    /// <param name="deviceCodeCallback">
    /// Déclenché une seule fois, au moment où une connexion interactive est nécessaire, avec le
    /// code à saisir et l'URL de vérification — à afficher de façon très visible (pas seulement
    /// dans un journal), car c'est la seule étape qui demande une action immédiate de l'utilisateur.
    /// </param>
    Task<MinecraftSession> GetActiveSessionAsync(
        IProgress<string>? progress = null,
        IProgress<DeviceCodeInfo>? deviceCodeCallback = null,
        CancellationToken cancellationToken = default);
}
