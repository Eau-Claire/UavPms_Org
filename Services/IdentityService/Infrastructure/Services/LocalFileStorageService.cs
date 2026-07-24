using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    /// <summary>
    /// Allowed file extensions as a secondary validation layer (primary: MIME type check in controller).
    /// </summary>
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif",
        ".mp4", ".avi", ".mov", ".webm"
    };

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var rawPath = configuration["FileStorage:AlertImagesPath"] ?? "uav_storage/images";
        _storagePath = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(Directory.GetCurrentDirectory(), rawPath);

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> SaveImageAsync(Stream fileStream, string fileName)
    {
        // 1. Sanitize: extract only the file name portion (防 path traversal like ../../etc/passwd)
        var safeFileName = Path.GetFileName(fileName);

        // 2. Validate file extension as a secondary security check
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"File extension '{extension}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");
        }

        // 3. Sanitize: replace spaces and non-safe characters with underscores
        //    Keep only alphanumeric, hyphens, underscores, and dots
        safeFileName = Regex.Replace(safeFileName, @"[^\w\-.]", "_");

        // 4. Build unique filename with GUID prefix to prevent collisions
        var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var filePath = Path.Combine(_storagePath, uniqueFileName);

        // 5. Final safety check: ensure resolved path is within the storage directory
        var resolvedPath = Path.GetFullPath(filePath);
        var resolvedStorage = Path.GetFullPath(_storagePath);
        if (!resolvedPath.StartsWith(resolvedStorage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attempted path traversal detected.");
        }

        await using var outputStream = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(outputStream);

        _logger.LogInformation("Saved image to {FilePath}", filePath);

        // 6. Return URL-encoded relative path for serving via Static Files middleware
        var encodedFileName = Uri.EscapeDataString(uniqueFileName);
        return $"/images/{encodedFileName}";
    }

    public Task DeleteImageAsync(string imagePath)
    {
        // imagePath is a relative path like /images/filename (may be URL-encoded)
        var rawFileName = Path.GetFileName(imagePath);
        var decodedFileName = Uri.UnescapeDataString(rawFileName);
        var fullPath = Path.Combine(_storagePath, decodedFileName);

        // Safety check: prevent path traversal in deletion
        var resolvedPath = Path.GetFullPath(fullPath);
        var resolvedStorage = Path.GetFullPath(_storagePath);
        if (!resolvedPath.StartsWith(resolvedStorage, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Attempted path traversal in deletion: {ImagePath}", imagePath);
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted image at {FilePath}", fullPath);
        }

        return Task.CompletedTask;
    }
}
