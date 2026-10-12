using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.Shared.Contracts.Interfaces;

public interface ISystemSettingService
{
    Task<string> GetStringAsync(string key, string defaultValue, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default);
    Task<double> GetDoubleAsync(string key, double defaultValue, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default);

    Task<OtpSettingsDto> GetOtpSettingsAsync(CancellationToken ct = default);
    Task<TokenSettingsDto> GetTokenSettingsAsync(CancellationToken ct = default);
    Task<AiEvaluationSettingsDto> GetAiEvaluationSettingsAsync(CancellationToken ct = default);
    Task<DroneThresholdSettingsDto> GetDroneThresholdSettingsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(string? category = null, CancellationToken ct = default);
    Task<SystemSettingDto?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<SystemSettingDto> UpdateAsync(string key, string value, CancellationToken ct = default);
    void InvalidateCache();
}
