using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftLauncherPerso.Services.Java;

/// <summary>Résultat de résolution : URL de téléchargement + nom de fichier de l'archive Temurin.</summary>
public sealed record JavaDownloadInfo(string DownloadUrl, string FileName);

/// <summary>Client minimal pour l'API Adoptium (https://api.adoptium.net), utilisé pour récupérer
/// la dernière build Temurin 8 (JRE) correspondant à l'OS/architecture de la machine.</summary>
public sealed class AdoptiumApiClient
{
    private const string ApiBaseUrl = "https://api.adoptium.net/v3";
    private readonly HttpClient _httpClient;

    public AdoptiumApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<JavaDownloadInfo> GetLatestJre8Async(CancellationToken cancellationToken = default)
    {
        var (os, architecture) = GetCurrentPlatform();
        var requestUrl = $"{ApiBaseUrl}/assets/latest/8/hotspot" +
                          $"?architecture={architecture}&image_type=jre&os={os}&vendor=eclipse";

        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var assets = await JsonSerializer.DeserializeAsync<List<AdoptiumAsset>>(stream, cancellationToken: cancellationToken);

        var asset = assets?.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Aucune build Temurin 8 disponible pour os={os} architecture={architecture}.");

        return new JavaDownloadInfo(asset.Binary.Package.Link, asset.Binary.Package.Name);
    }

    public async Task DownloadAsync(
        string url,
        string destinationPath,
        Action<double>? onProgress,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                onProgress?.Invoke((double)totalRead / totalBytes);
            }
        }
    }

    private static (string os, string architecture) GetCurrentPlatform()
    {
        var os = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => "windows",
            PlatformID.Unix when OperatingSystem.IsMacOS() => "mac",
            PlatformID.Unix => "linux",
            _ => throw new PlatformNotSupportedException("Plateforme non supportée pour le téléchargement de Java."),
        };

        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86-32",
            Architecture.Arm64 => "aarch64",
            _ => throw new PlatformNotSupportedException("Architecture processeur non supportée."),
        };

        return (os, architecture);
    }

    private sealed class AdoptiumAsset
    {
        [JsonPropertyName("binary")]
        public AdoptiumBinary Binary { get; set; } = null!;
    }

    private sealed class AdoptiumBinary
    {
        [JsonPropertyName("package")]
        public AdoptiumPackage Package { get; set; } = null!;
    }

    private sealed class AdoptiumPackage
    {
        [JsonPropertyName("link")]
        public string Link { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
