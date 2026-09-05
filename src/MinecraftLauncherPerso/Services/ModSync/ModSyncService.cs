using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftLauncherPerso.Services.ModSync;

/// <summary>
/// Synchronise le pack de mods/config depuis une archive .zip unique hébergée sur le VPS
/// (ex. http://185.185.82.180/modpack/Algaron-modded.zip), qui doit contenir mods/ et config/
/// à sa racine (mêmes noms de dossiers qu'un .minecraft classique).
///
/// Ne retélécharge que si le fichier a changé côté serveur : une requête HEAD récupère
/// ETag/Last-Modified/Content-Length, comparés à la dernière synchro réussie (mise en cache
/// localement) — pas de re-téléchargement à chaque lancement. Si le serveur ne supporte pas
/// HEAD (config VPS basique), on retélécharge par prudence plutôt que d'échouer.
/// </summary>
public sealed class ModSyncService : IModSyncService
{
    private const string CacheFileName = "launcher-modpack-cache.json";

    private readonly HttpClient _httpClient;

    public ModSyncService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task SyncAsync(
        string modpackZipUrl,
        string gameDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modpackZipUrl))
        {
            throw new InvalidOperationException(
                "ModpackZipUrl n'est pas configuré (settings.json) : impossible de synchroniser le modpack.");
        }

        progress?.Report("Vérification de la version du modpack...");

        RemoteZipMetadata? remoteMetadata;
        try
        {
            remoteMetadata = await FetchRemoteMetadataAsync(modpackZipUrl, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Serveur ne supportant peut-être pas HEAD : on retélécharge par prudence.
            remoteMetadata = null;
        }

        var cachePath = Path.Combine(gameDirectory, CacheFileName);
        var cached = LoadCache(cachePath);

        var upToDate = remoteMetadata is not null
            && cached is not null
            && cached.Matches(remoteMetadata)
            && Directory.Exists(Path.Combine(gameDirectory, "mods"));

        if (upToDate)
        {
            progress?.Report("Modpack déjà à jour.");
            return;
        }

        progress?.Report("Téléchargement du modpack...");
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"modpack-{Guid.NewGuid():N}.zip");

        try
        {
            await DownloadAsync(modpackZipUrl, tempZipPath, progress, cancellationToken);

            progress?.Report("Extraction du modpack (mods/config)...");
            Directory.CreateDirectory(gameDirectory);
            ZipFile.ExtractToDirectory(tempZipPath, gameDirectory, overwriteFiles: true);
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }

        if (remoteMetadata is not null)
        {
            SaveCache(cachePath, remoteMetadata);
        }

        progress?.Report("Modpack mis à jour.");
    }

    private async Task<RemoteZipMetadata> FetchRemoteMetadataAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new RemoteZipMetadata
        {
            ETag = response.Headers.ETag?.Tag,
            LastModified = response.Content.Headers.LastModified,
            ContentLength = response.Content.Headers.ContentLength,
        };
    }

    private async Task DownloadAsync(string url, string destinationPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        var lastReportedPercent = -1;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)(totalRead * 100 / totalBytes);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report($"Téléchargement du modpack... {percent}%");
                }
            }
        }
    }

    private static RemoteZipMetadata? LoadCache(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemoteZipMetadata>(File.ReadAllText(cachePath));
        }
        catch (JsonException)
        {
            // Cache local corrompu : on ignore, le modpack sera retéléchargé et le cache recréé.
            return null;
        }
    }

    private static void SaveCache(string cachePath, RemoteZipMetadata metadata)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(cachePath, JsonSerializer.Serialize(metadata));
    }

    private sealed class RemoteZipMetadata
    {
        [JsonPropertyName("etag")]
        public string? ETag { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTimeOffset? LastModified { get; set; }

        [JsonPropertyName("contentLength")]
        public long? ContentLength { get; set; }

        public bool Matches(RemoteZipMetadata other)
        {
            // Si le serveur fournit un ETag, c'est le signal le plus fiable : on s'y fie seul.
            if (ETag is not null || other.ETag is not null)
            {
                return ETag == other.ETag;
            }

            return LastModified == other.LastModified && ContentLength == other.ContentLength;
        }
    }
}
