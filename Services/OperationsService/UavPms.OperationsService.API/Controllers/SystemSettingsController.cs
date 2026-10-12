using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/system-settings")]
[ApiVersion("1.0")]
[Authorize]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingService _settingService;

    public SystemSettingsController(ISystemSettingService settingService)
    {
        _settingService = settingService;
    }

    /// <summary>
    /// Lấy danh sách tất cả cấu hình ngưỡng hệ thống (cho phép lọc theo category)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> GetAllSettings([FromQuery] string? category, CancellationToken ct)
    {
        var settings = await _settingService.GetAllAsync(category, ct);
        return Ok(new ApiResponse(true, "Lấy danh sách cấu hình hệ thống thành công", settings));
    }

    /// <summary>
    /// Lấy chi tiết một cấu hình ngưỡng theo Key
    /// </summary>
    [HttpGet("{key}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> GetSettingByKey(string key, CancellationToken ct)
    {
        var setting = await _settingService.GetByKeyAsync(key, ct);
        if (setting == null)
        {
            return NotFound(new ApiResponse(false, $"Không tìm thấy cấu hình với mã: {key}", ErrorCode: "SETTING_NOT_FOUND"));
        }

        return Ok(new ApiResponse(true, "Lấy thông tin cấu hình thành công", setting));
    }

    /// <summary>
    /// Cập nhật giá trị ngưỡng hệ thống (Chỉ dành cho SystemAdmin)
    /// </summary>
    [HttpPut("{key}")]
    [Authorize(Roles = UserRoles.SystemAdmin)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSystemSettingRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Value))
        {
            return BadRequest(new ApiResponse(false, "Giá trị cấu hình không được để trống", ErrorCode: "INVALID_VALUE"));
        }

        try
        {
            var updatedSetting = await _settingService.UpdateAsync(key, request.Value, ct);
            return Ok(new ApiResponse(true, $"Cập nhật cấu hình '{key}' thành công", updatedSetting));
        }
        catch (System.Exception ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message, ErrorCode: "UPDATE_SETTING_FAILED"));
        }
    }

    /// <summary>
    /// Buộc làm mới bộ đệm (cache) cấu hình hệ thống
    /// </summary>
    [HttpPost("reload")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public IActionResult ReloadCache()
    {
        _settingService.InvalidateCache();
        return Ok(new ApiResponse(true, "Bộ đệm cấu hình hệ thống đã được làm mới thành công"));
    }
}
