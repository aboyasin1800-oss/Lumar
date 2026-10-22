using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace LUMAR_ERP_API_V2.Services;

public sealed record CampaignMediaUploadResult(
    string FileName,
    string RelativeUrl,
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

    public CampaignMediaStorageService(string? rootPath = null, ILogger<CampaignMediaStorageService>? logger = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Environment.CurrentDirectory, "wwwroot", "campaign-media")
            : rootPath;
        _logger = logger;

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

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.Trim();
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
        return new CampaignMediaUploadResult(
            uniqueName,
            relativeUrl,
            contentType,
            bytes.LongLength,
            targetPath,
            originalName);
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
