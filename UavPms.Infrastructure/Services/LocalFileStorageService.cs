using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<LocalFileStorageService> _logger;

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
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(_storagePath, uniqueFileName);

        await using var outputStream = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(outputStream);

        _logger.LogInformation("Saved image to {FilePath}", filePath);

        // Trả về đường dẫn tương đối để phục vụ qua Static Files middleware
        return $"/images/{uniqueFileName}";
    }

    public Task DeleteImageAsync(string imagePath)
    {
        // imagePath là đường dẫn tương đối dạng /images/filename
        var fileName = Path.GetFileName(imagePath);
        var fullPath = Path.Combine(_storagePath, fileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted image at {FilePath}", fullPath);
        }

        return Task.CompletedTask;
    }
}
