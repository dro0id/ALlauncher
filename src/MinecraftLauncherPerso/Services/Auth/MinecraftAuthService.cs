namespace MinecraftLauncherPerso.Services.Auth;

/// <summary>
/// TODO (prochaine étape) : lire %AppData%/.minecraft/launcher_accounts.json, prendre le compte
/// référencé par "activeAccountLocalId", et en extraire username/uuid/accessToken. Attention :
/// le format exact du fichier a changé selon les versions du launcher officiel (migration MSA) ;
/// à valider contre un launcher_accounts.json réel avant implémentation finale.
/// </summary>
public sealed class MinecraftAuthService : IAuthService
{
    public MinecraftSession GetActiveSession()
    {
        throw new NotImplementedException("Lecture de la session du launcher officiel à implémenter (voir README, étape suivante).");
    }
}
