using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.NotificationService.Infrastructure.Persistence;

namespace UavPms.NotificationService.API.Jobs;

public class CleanupJob(ILogger<CleanupJob> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
{
    private readonly ILogger<CleanupJob> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task Execute()
    {
        _logger.LogInformation("Auto-Cleanup job started: Purging stored files older than 30 days and expired/revoked tokens...");

        var imagePath = _configuration["FileStorage:AlertImagesPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images");
        
        try
        {
            if (!string.IsNullOrEmpty(_configuration["FileStorage:AlertImagesPath"]))
            {
                var dir = new DirectoryInfo(imagePath);

                _ = dir.GetFiles();
            }
        }
        catch
        {
            imagePath = Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images");
        }

        try
        {
            if (Directory.Exists(imagePath))
            {
                var directoryInfo = new DirectoryInfo(imagePath);
                var thresholdDate = DateTime.UtcNow.AddDays(-30);
                int deletedCount = 0;

                foreach (var file in directoryInfo.GetFiles())
                {
                    if (file.CreationTimeUtc < thresholdDate)
                    {
                        file.Delete();
                        deletedCount++;
                    }
                }

                _logger.LogInformation("Auto-Cleanup: Purged {Count} files older than 30 days.", deletedCount);
            }
            else
            {
                _logger.LogWarning("Auto-Cleanup: Storage directory '{Path}' does not exist. Skipping file purge.", imagePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during file Auto-Cleanup.");
        }

        // Database Cleanup
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var thresholdDate = DateTime.UtcNow.AddDays(-30);

            // Xóa RefreshTokens hết hạn hoặc đã bị thu hồi quá 30 ngày
            var oldTokens = await dbContext.RefreshTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow || t.RevokedAt < thresholdDate)
                .ToListAsync();

            if (oldTokens.Any())
            {
                dbContext.RefreshTokens.RemoveRange(oldTokens);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Auto-Cleanup: Purged {Count} expired or revoked refresh tokens from database.", oldTokens.Count);
            }
            else
            {
                _logger.LogInformation("Auto-Cleanup: No expired or revoked refresh tokens to purge.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database token Auto-Cleanup.");
        }
    }
}
