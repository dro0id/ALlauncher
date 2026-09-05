using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace MinecraftLauncherPerso.Services.ModSync;

/// <summary>
/// Synchronise mods/ et config/ depuis un manifeste JSON hébergé sur le VPS
/// (GET {modsServerBaseUrl}/manifest.json). Ne télécharge que les fichiers absents ou dont le
/// hash a changé depuis la dernière synchro : la comparaison se fait contre un manifeste local
/// mis en cache (pas en re-hashant tout le disque à chaque lancement), avec juste une vérification
/// d'existence du fichier en plus du hash. Supprime aussi les fichiers qui ne sont plus référencés
/// par le manifeste distant (mod retiré du pack).
/// </summary>
public sealed class ModSyncService : IModSyncService
{
    private const string ManifestFileName = "manifest.json";
    private const string LocalCacheFileName = "launcher-mods-manifest.json";

    private readonly HttpClient _httpClient;

    public ModSyncService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task SyncAsync(
        string modsServerBaseUrl,
        string gameDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modsServerBaseUrl))
        {
            throw new InvalidOperationException(
                "ModsServerBaseUrl n'est pas configuré (settings.json) : impossible de synchroniser mods/config.");
        }

        progress?.Report("Vérification des mises à jour de mods/config...");

        var remoteManifest = await DownloadManifestAsync(modsServerBaseUrl, cancellationToken);
        var cachePath = Path.Combine(gameDirectory, LocalCacheFileName);
        var cachedManifest = LoadCachedManifest(cachePath);
        var cachedByPath = cachedManifest?.Files.ToDictionary(f => f.Path) ?? new Dictionary<string, ModFileEntry>();

        var toDownload = remoteManifest.Files.Where(entry =>
        {
            var localFilePath = Path.Combine(gameDirectory, entry.Path);
            var upToDate = cachedByPath.TryGetValue(entry.Path, out var cachedEntry)
                && string.Equals(cachedEntry.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                && File.Exists(localFilePath);

            return !upToDate;
        }).ToList();

        for (var i = 0; i < toDownload.Count; i++)
        {
            var entry = toDownload[i];
            progress?.Report($"Téléchargement {i + 1}/{toDownload.Count} : {entry.Path}");
            await DownloadFileAsync(modsServerBaseUrl, entry, gameDirectory, cancellationToken);
        }

        RemoveObsoleteFiles(gameDirectory, remoteManifest, cachedManifest);
        SaveManifest(cachePath, remoteManifest);

        progress?.Report(toDownload.Count == 0
            ? "mods/ et config/ déjà à jour."
            : $"{toDownload.Count} fichier(s) mis à jour.");
    }

    private async Task<ModManifest> DownloadManifestAsync(string modsServerBaseUrl, CancellationToken cancellationToken)
    {
        var manifestUrl = CombineUrl(modsServerBaseUrl, ManifestFileName);

        await using var stream = await _httpClient.GetStreamAsync(manifestUrl, cancellationToken);
        return await JsonSerializer.DeserializeAsync<ModManifest>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Manifeste invalide reçu depuis {manifestUrl}.");
    }

    private static ModManifest? LoadCachedManifest(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(cachePath));
        }
        catch (JsonException)
        {
            // Cache local corrompu : on ignore, tout sera re-téléchargé et le cache sera recréé.
            return null;
        }
    }

    private static void SaveManifest(string cachePath, ModManifest manifest)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(cachePath, JsonSerializer.Serialize(manifest));
    }

    private async Task DownloadFileAsync(string modsServerBaseUrl, ModFileEntry entry, string gameDirectory, CancellationToken cancellationToken)
    {
        var fileUrl = CombineUrl(modsServerBaseUrl, entry.Url);
        var destinationPath = Path.Combine(gameDirectory, entry.Path);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";
        await using (var responseStream = await _httpClient.GetStreamAsync(fileUrl, cancellationToken))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await responseStream.CopyToAsync(fileStream, cancellationToken);
        }

        VerifyHash(tempPath, entry);

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private static void VerifyHash(string filePath, ModFileEntry entry)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();

        if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(filePath);
            throw new InvalidOperationException(
                $"Hash invalide pour {entry.Path} après téléchargement (attendu {entry.Sha256}, obtenu {hash}). " +
                "Fichier VPS corrompu ou manifeste désynchronisé.");
        }
    }

    private static void RemoveObsoleteFiles(string gameDirectory, ModManifest remoteManifest, ModManifest? cachedManifest)
    {
        if (cachedManifest is null)
        {
            return;
        }

        var remotePaths = remoteManifest.Files.Select(f => f.Path).ToHashSet();

        foreach (var cachedEntry in cachedManifest.Files)
        {
            if (remotePaths.Contains(cachedEntry.Path))
            {
                continue;
            }

            var obsoletePath = Path.Combine(gameDirectory, cachedEntry.Path);
            if (File.Exists(obsoletePath))
            {
                File.Delete(obsoletePath);
            }
        }
    }

    private static string CombineUrl(string baseUrl, string relativePath)
        => $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
}
