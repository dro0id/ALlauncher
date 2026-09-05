using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftLauncherPerso.Services.Auth;

/// <summary>
/// Lit la session active du launcher officiel Minecraft installé sur la machine
/// (%AppData%/.minecraft/launcher_accounts.json), sans implémenter de flux OAuth Microsoft :
/// l'utilisateur doit simplement être déjà connecté dans le launcher officiel.
///
/// Format actuel (post-migration comptes Microsoft) : un dictionnaire "accounts" indexé par
/// localId, chaque compte exposant "accessToken" + "minecraftProfile" (name/id). Piège à éviter :
/// le champ racine "username" d'un compte est l'identifiant Microsoft (email), PAS le pseudo
/// Minecraft — le pseudo et l'UUID utilisés en jeu viennent de minecraftProfile.name/.id.
/// </summary>
public sealed class MinecraftAuthService : IAuthService
{
    private readonly string _launcherAccountsPath;

    public MinecraftAuthService(string? launcherAccountsPath = null)
    {
        _launcherAccountsPath = launcherAccountsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "launcher_accounts.json");
    }

    public MinecraftSession GetActiveSession()
    {
        if (!File.Exists(_launcherAccountsPath))
        {
            throw new InvalidOperationException(
                $"Fichier introuvable : {_launcherAccountsPath}. " +
                "Ouvrez le launcher officiel Minecraft et connectez-vous d'abord avec votre compte.");
        }

        var root = JsonSerializer.Deserialize<LauncherAccountsFile>(File.ReadAllText(_launcherAccountsPath))
            ?? throw new InvalidOperationException($"Fichier illisible : {_launcherAccountsPath}.");

        if (string.IsNullOrEmpty(root.ActiveAccountLocalId)
            || root.Accounts is null
            || !root.Accounts.TryGetValue(root.ActiveAccountLocalId, out var account))
        {
            throw new InvalidOperationException(
                "Aucun compte actif trouvé dans launcher_accounts.json. " +
                "Connectez-vous dans le launcher officiel Minecraft, puis relancez ce launcher.");
        }

        if (account.MinecraftProfile is null || string.IsNullOrEmpty(account.AccessToken))
        {
            throw new InvalidOperationException(
                "Le compte actif du launcher officiel n'a pas de profil Minecraft ou d'accessToken valide.");
        }

        if (account.AccessTokenExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException(
                "La session du launcher officiel a expiré. Rouvrez-le, reconnectez-vous, puis relancez ce launcher.");
        }

        return new MinecraftSession(
            account.MinecraftProfile.Name,
            FormatUuid(account.MinecraftProfile.Id),
            account.AccessToken);
    }

    /// <summary>launcher_accounts.json stocke l'UUID sans tirets ; CmlLib.Core attend le format standard 8-4-4-4-12.</summary>
    private static string FormatUuid(string rawId)
    {
        var clean = rawId.Replace("-", "");
        if (clean.Length != 32)
        {
            return rawId; // Format inattendu : on le laisse tel quel plutôt que de planter ici.
        }

        return $"{clean[..8]}-{clean[8..12]}-{clean[12..16]}-{clean[16..20]}-{clean[20..]}";
    }

    private sealed class LauncherAccountsFile
    {
        [JsonPropertyName("accounts")]
        public Dictionary<string, LauncherAccount>? Accounts { get; set; }

        [JsonPropertyName("activeAccountLocalId")]
        public string? ActiveAccountLocalId { get; set; }
    }

    private sealed class LauncherAccount
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("accessTokenExpiresAt")]
        public DateTimeOffset? AccessTokenExpiresAt { get; set; }

        [JsonPropertyName("minecraftProfile")]
        public MinecraftProfile? MinecraftProfile { get; set; }
    }

    private sealed class MinecraftProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
