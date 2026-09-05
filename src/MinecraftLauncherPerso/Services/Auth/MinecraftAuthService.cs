using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftLauncherPerso.Services.Auth;

/// <summary>
/// Lit la session active du launcher officiel Minecraft installé sur la machine, sans
/// implémenter de flux OAuth Microsoft : l'utilisateur doit simplement être déjà connecté dans
/// le launcher officiel (classique ou Microsoft Store/Xbox).
///
/// Deux fichiers possibles dans %AppData%/.minecraft/, selon la variante du launcher officiel
/// installée (même format JSON dans les deux cas) :
///   - launcher_accounts_microsoft_store.json : launcher installé depuis le Microsoft Store / app Xbox.
///   - launcher_accounts.json : launcher classique téléchargé sur minecraft.net.
/// Les deux sont essayés dans cet ordre, car la variante Store est aujourd'hui la plus courante
/// sur une installation Windows récente.
///
/// Format (post-migration comptes Microsoft) : un dictionnaire "accounts" indexé par localId,
/// chaque compte exposant "accessToken" + "minecraftProfile" (name/id). Piège à éviter : le champ
/// racine "username" d'un compte est l'identifiant Microsoft (email), PAS le pseudo Minecraft —
/// le pseudo et l'UUID utilisés en jeu viennent de minecraftProfile.name/.id.
/// </summary>
public sealed class MinecraftAuthService : IAuthService
{
    private static readonly string[] CandidateFileNames =
    {
        "launcher_accounts_microsoft_store.json",
        "launcher_accounts.json",
    };

    private readonly IReadOnlyList<string> _candidatePaths;

    public MinecraftAuthService(string? launcherAccountsPath = null)
    {
        if (launcherAccountsPath is not null)
        {
            _candidatePaths = new[] { launcherAccountsPath };
            return;
        }

        var minecraftDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft");

        _candidatePaths = CandidateFileNames
            .Select(name => Path.Combine(minecraftDirectory, name))
            .ToArray();
    }

    public MinecraftSession GetActiveSession()
    {
        var problems = new List<string>();

        foreach (var path in _candidatePaths)
        {
            if (!File.Exists(path))
            {
                problems.Add($"{path} : fichier introuvable.");
                continue;
            }

            if (TryReadSession(path, out var session, out var problem))
            {
                return session;
            }

            problems.Add($"{path} : {problem}");
        }

        throw new InvalidOperationException(
            "Aucune session valide trouvée dans le launcher officiel Minecraft (classique ou " +
            "Microsoft Store). Ouvrez-le et connectez-vous d'abord avec votre compte.\n" +
            string.Join("\n", problems));
    }

    private static bool TryReadSession(string path, out MinecraftSession session, out string? problem)
    {
        session = null!;

        LauncherAccountsFile? root;
        try
        {
            root = JsonSerializer.Deserialize<LauncherAccountsFile>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            problem = "fichier illisible (JSON invalide).";
            return false;
        }

        if (root is null)
        {
            problem = "fichier illisible.";
            return false;
        }

        if (string.IsNullOrEmpty(root.ActiveAccountLocalId)
            || root.Accounts is null
            || !root.Accounts.TryGetValue(root.ActiveAccountLocalId, out var account))
        {
            problem = "aucun compte actif.";
            return false;
        }

        if (account.MinecraftProfile is null || string.IsNullOrEmpty(account.AccessToken))
        {
            problem = "profil Minecraft ou accessToken manquant.";
            return false;
        }

        if (account.AccessTokenExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow)
        {
            problem = "session expirée : reconnectez-vous dans le launcher officiel.";
            return false;
        }

        problem = null;
        session = new MinecraftSession(
            account.MinecraftProfile.Name,
            FormatUuid(account.MinecraftProfile.Id),
            account.AccessToken);
        return true;
    }

    /// <summary>launcher_accounts*.json stocke l'UUID sans tirets ; CmlLib.Core attend le format standard 8-4-4-4-12.</summary>
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
