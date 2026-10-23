using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace LUMAR_ERP_API_V2.Services;

public sealed record CampaignMediaUploadResult(
    string FileName,
    string RelativeUrl,
    string CampaignImageUrl,
    string ContentType,
    long Length,
    string StoragePath,
    string OriginalFileName);

public sealed class CampaignMediaStorageService
{
    private const long MaxBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };

    private readonly string _rootPath;
    private readonly ILogger<CampaignMediaStorageService>? _logger;
    private readonly string _publicBaseUrl;

    public CampaignMediaStorageService(string? rootPath = null, ILogger<CampaignMediaStorageService>? logger = null, string? publicBaseUrl = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Environment.CurrentDirectory, "wwwroot", "campaign-media")
            : rootPath;
        _logger = logger;
        _publicBaseUrl = publicBaseUrl ??
            Environment.GetEnvironmentVariable("LUMAR_CAMPAIGN_MEDIA_PUBLIC_BASE_URL") ??
            Environment.GetEnvironmentVariable("LUMAR_PUBLIC_BACKEND_BASE_URL") ??
            string.Empty;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<CampaignMediaUploadResult> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
            throw new InvalidOperationException("Campaign image file is required.");

        if (file.Length <= 0 || file.Length > MaxBytes)
            throw new InvalidOperationException("Campaign image file must be between 1 byte and 8 MB.");

        var originalName = Path.GetFileName(file.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Campaign image file name is invalid.");

        if (originalName.Contains("..", StringComparison.Ordinal) || originalName.Contains('/'))
            throw new InvalidOperationException("Campaign image file name is unsafe.");

        var contentTypeValue = file.ContentType ?? (file.Headers is not null && file.Headers.ContainsKey("Content-Type") ? file.Headers["Content-Type"].ToString() : null);
        var contentType = string.IsNullOrWhiteSpace(contentTypeValue) ? "application/octet-stream" : contentTypeValue.Trim();
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException("Campaign image file type is not allowed.");

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (!HasImageSignature(bytes))
            throw new InvalidOperationException("Campaign image file content is not a valid supported image.");

        var extension = Path.GetExtension(originalName).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !IsSupportedExtension(extension))
            throw new InvalidOperationException("Unsupported campaign image extension.");

        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = Path.Combine(_rootPath, uniqueName);

        try
        {
            await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
        }
        catch (IOException ex)
        {
            _logger?.LogError(ex, "Failed to persist campaign file to {Path}", targetPath);
            throw new InvalidOperationException("Campaign image storage failed.", ex);
        }

        var relativeUrl = "/campaign-media/" + uniqueName;
        var absoluteUrl = BuildPublicCampaignImageUrl(uniqueName);

        return new CampaignMediaUploadResult(
            uniqueName,
            relativeUrl,
            absoluteUrl,
            contentType,
            bytes.LongLength,
            targetPath,
            originalName);
    }

    private string BuildPublicCampaignImageUrl(string uniqueFileName)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_publicBaseUrl)
            ? Environment.GetEnvironmentVariable("LUMAR_CAMPAIGN_MEDIA_PUBLIC_BASE_URL") ??
              Environment.GetEnvironmentVariable("LUMAR_PUBLIC_BACKEND_BASE_URL") ??
              string.Empty
            : _publicBaseUrl;

        if (!TryCreatePublicBaseUri(baseUrl, out var publicBaseUri))
            throw new InvalidOperationException("Campaign image public base URL is not configured or is invalid. Set CampaignMedia:PublicBaseUrl to an HTTPS public backend URL.");

        var path = publicBaseUri.AbsolutePath.TrimEnd('/') + "/campaign-media/" + uniqueFileName;
        return new Uri(publicBaseUri, path).ToString();
    }

    private static bool TryCreatePublicBaseUri(string? rawBaseUrl, out Uri publicBaseUri)
    {
        publicBaseUri = null!;
        if (string.IsNullOrWhiteSpace(rawBaseUrl))
            return false;

        var trimmed = rawBaseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var baseUri) || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (baseUri.IsLoopback || string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        if (baseUri.Host.StartsWith("127.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IPAddress.TryParse(baseUri.Host, out var ipAddress) && (ipAddress.Equals(IPAddress.Loopback) || IsPrivateAddress(ipAddress)))
            return false;

        publicBaseUri = baseUri;
        return true;
    }

    private static bool IsPrivateAddress(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return bytes[0] switch
            {
                10 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                127 => true,
                _ => false
            };
        }

        return ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || IPAddress.IsLoopback(ipAddress);
    }

    private static bool IsSupportedExtension(string extension) => extension switch
    {
        ".png" => true,
        ".jpg" or ".jpeg" => true,
        ".webp" => true,
        ".gif" => true,
        _ => false
    };

    private static bool HasImageSignature(byte[] bytes)
    {
        if (bytes.Length < 8)
            return false;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return true;

        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes.Length >= 12)
        {
            var webp = System.Text.Encoding.ASCII.GetString(bytes, 8, 4);
            return webp == "WEBP";
        }

        return false;
    }
}
