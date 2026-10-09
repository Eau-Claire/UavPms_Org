using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.IdentityService.Infrastructure.Services;

public class SystemSettingService : ISystemSettingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SystemSettingService> _logger;
    private static readonly ConcurrentDictionary<string, (string Value, DateTime ExpiresAt)> Cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public SystemSettingService(ApplicationDbContext db, ILogger<SystemSettingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void InvalidateCache()
    {
        Cache.Clear();
    }

    public async Task<string> GetStringAsync(string key, string defaultValue, CancellationToken ct = default)
    {
        if (Cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Value;
        }

        try
        {
            var setting = await _db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, ct);

            var value = setting != null && !string.IsNullOrWhiteSpace(setting.Value)
                ? setting.Value
                : defaultValue;

            Cache[key] = (value, DateTime.UtcNow.Add(CacheDuration));
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch SystemSetting for key {Key} from database. Falling back to default: {Default}", key, defaultValue);
            return defaultValue;
        }
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture), ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    public async Task<double> GetDoubleAsync(string key, double defaultValue, CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture), ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, defaultValue.ToString(), ct);
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public async Task<OtpSettingsDto> GetOtpSettingsAsync(CancellationToken ct = default)
    {
        var expiryMinutes = await GetIntAsync(SettingKeys.OtpExpiryMinutes, 15, ct);
        var cooldownSeconds = await GetIntAsync(SettingKeys.OtpCooldownSeconds, 30, ct);
        var maxAttempts = await GetIntAsync(SettingKeys.OtpMaxAttempts, 5, ct);
        var bypassCode = await GetStringAsync(SettingKeys.OtpBypassCode, "123456", ct);

        return new OtpSettingsDto(expiryMinutes, cooldownSeconds, maxAttempts, bypassCode);
    }

    public async Task<TokenSettingsDto> GetTokenSettingsAsync(CancellationToken ct = default)
    {
        var accessExpiry = await GetIntAsync(SettingKeys.AuthAccessTokenExpiryMinutes, 60, ct);
        var refreshDays = await GetIntAsync(SettingKeys.AuthRefreshTokenExpiryDays, 7, ct);
        var deviceTrustDays = await GetIntAsync(SettingKeys.AuthDeviceTrustDays, 30, ct);
        var stepUpMinutes = await GetIntAsync(SettingKeys.AuthStepUpTokenExpiryMinutes, 5, ct);
        var verificationMinutes = await GetIntAsync(SettingKeys.AuthVerificationTokenExpiryMinutes, 10, ct);

        return new TokenSettingsDto(accessExpiry, refreshDays, deviceTrustDays, stepUpMinutes, verificationMinutes);
    }

    public async Task<AiEvaluationSettingsDto> GetAiEvaluationSettingsAsync(CancellationToken ct = default)
    {
        var emergencyConfidence = await GetDoubleAsync(SettingKeys.AiEmergencyConfidenceThreshold, 0.80, ct);
        var criticalScore = await GetIntAsync(SettingKeys.AiCriticalScoreThreshold, 90, ct);
        var highScore = await GetIntAsync(SettingKeys.AiHighScoreThreshold, 75, ct);
        var mediumScore = await GetIntAsync(SettingKeys.AiMediumScoreThreshold, 50, ct);
        var emergencyWeight = await GetIntAsync(SettingKeys.AiEmergencyWeight, 45, ct);
        var maxConfidenceWeight = await GetIntAsync(SettingKeys.AiMaxConfidenceWeight, 55, ct);
        var defaultModel = await GetStringAsync(SettingKeys.AiDefaultPreferredModel, "SERVER", ct);
        var timeoutSeconds = await GetIntAsync(SettingKeys.AiTimeoutSeconds, 60, ct);

        return new AiEvaluationSettingsDto(
            emergencyConfidence, criticalScore, highScore, mediumScore,
            emergencyWeight, maxConfidenceWeight, defaultModel, timeoutSeconds);
    }

    public async Task<DroneThresholdSettingsDto> GetDroneThresholdSettingsAsync(CancellationToken ct = default)
    {
        var criticalBattery = await GetDoubleAsync(SettingKeys.DroneCriticalBatteryThreshold, 30.0, ct);
        var warningBattery = await GetDoubleAsync(SettingKeys.DroneWarningBatteryThreshold, 50.0, ct);
        var minRfLink = await GetDoubleAsync(SettingKeys.DroneMinRfLinkThreshold, 50.0, ct);
        var validityDays = await GetIntAsync(SettingKeys.DroneInspectionValidityDays, 7, ct);

        return new DroneThresholdSettingsDto(criticalBattery, warningBattery, minRfLink, validityDays);
    }

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(string? category = null, CancellationToken ct = default)
    {
        var query = _db.SystemSettings.AsNoTracking().Where(s => !s.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var list = await query.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync(ct);
        return list.Select(s => new SystemSettingDto(
            s.Id, s.Key, s.Value, s.Description, s.Category, s.DataType, s.UpdatedAt
        )).ToList();
    }

    public async Task<SystemSettingDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, ct);

        return setting == null ? null : new SystemSettingDto(
            setting.Id, setting.Key, setting.Value, setting.Description, setting.Category, setting.DataType, setting.UpdatedAt
        );
    }

    public async Task<SystemSettingDto> UpdateAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, ct);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            await _db.SystemSettings.AddAsync(setting, ct);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        Cache.TryRemove(key, out _);

        return new SystemSettingDto(
            setting.Id, setting.Key, setting.Value, setting.Description, setting.Category, setting.DataType, setting.UpdatedAt
        );
    }
}
