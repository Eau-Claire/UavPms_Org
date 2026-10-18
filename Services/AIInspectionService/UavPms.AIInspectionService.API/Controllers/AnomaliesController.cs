using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.AIInspectionService.API.Controllers;

public record AnomalyValidateRequestDto(
    string Status,
    string? AnalystNotes
);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/anomalies")]
[Authorize]
public class AnomaliesController : ControllerBase
{
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;

    public AnomaliesController(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUserServices)
    {
        _anomalyRepository = anomalyRepository;
        _unitOfWork = unitOfWork;
        _currentUserServices = currentUserServices;
    }

    /// <summary>
    /// Lấy danh sách các anomaly/phát hiện đang chờ thẩm định (Pending validation)
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public async Task<IActionResult> GetPendingAnomalies(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 12;

        var anomalies = await _anomalyRepository.GetPendingWithDetailsAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            anomalies = anomalies.Where(a =>
                (a.Category?.CategoryName != null && a.Category.CategoryName.ToLowerInvariant().Contains(s)) ||
                (a.TowerId != null && a.TowerId.ToLowerInvariant().Contains(s)) ||
                (a.Asset?.AssetCode != null && a.Asset.AssetCode.ToLowerInvariant().Contains(s))
            ).ToList();
        }

        var totalCount = anomalies.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pagedItems = anomalies
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id = a.Id.ToString(),
                mediaUrl = a.ImageUrl ?? a.Media?.FileUrl ?? "/images/defect-insulator-crack.png",
                assetCode = a.Asset?.AssetCode ?? "INS-TOW05-01",
                towerCode = a.TowerId ?? "TOW-220KV-042",
                missionCode = a.Media?.MissionId != null ? a.Media.MissionId.ToString() : "MIS-2026-001",
                missionName = "Khảo sát định kỳ tuyến 220kV",
                categoryName = a.Category?.CategoryName ?? "Bát cách điện nứt vỡ (Insulator Damage)",
                categoryCode = a.Category?.CategoryCode ?? "DEF-INS-CRACK",
                boundingBox = a.BoundingBox,
                confidenceScore = a.ConfidenceScore > 0 ? a.ConfidenceScore : 90,
                validationStatus = string.IsNullOrWhiteSpace(a.ValidationStatus) ? "Pending" : a.ValidationStatus,
                analystNotes = a.AnalystNotes,
                aiModelName = a.AiSource ?? "YOLOv8-PowerGrid-v2.4",
                createdAt = a.CreatedAt.ToString("o"),
                severity = a.Category?.SeverityWeight ?? 4,
                isEmergency = a.EmergencyAlerts != null && a.EmergencyAlerts.Count > 0
            })
            .ToList();

        return Ok(new ApiResponse(true, "Pending anomalies retrieved successfully.", new
        {
            items = pagedItems,
            totalCount,
            pageIndex,
            pageSize,
            totalPages
        }));
    }

    /// <summary>
    /// Thẩm định anomaly (Xác nhận Confirmed hoặc Bác bỏ Rejected)
    /// </summary>
    [HttpPut("{id:guid}/validate")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public async Task<IActionResult> ValidateAnomaly(
        [FromRoute] Guid id,
        [FromBody] AnomalyValidateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var anomaly = await _anomalyRepository.GetByIdWithDetailAsync(id);
        if (anomaly == null)
        {
            return NotFound(new ApiResponse(false, $"Anomaly with ID {id} was not found."));
        }

        var analystId = _currentUserServices.UserId;
        var notes = request.AnalystNotes ?? string.Empty;

        if (string.Equals(request.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            anomaly.Confirm(analystId, notes);
        }
        else if (string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(request.Status, "Dismissed", StringComparison.OrdinalIgnoreCase))
        {
            anomaly.Reject(analystId, notes);
        }
        else
        {
            anomaly.ValidationStatus = request.Status;
            anomaly.AnalystNotes = notes;
            anomaly.ValidatedAt = DateTime.UtcNow;
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse(true, "Anomaly validated successfully."));
    }
}
