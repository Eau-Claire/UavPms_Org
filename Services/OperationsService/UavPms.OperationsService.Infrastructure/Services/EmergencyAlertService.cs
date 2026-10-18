using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Features.Alerts;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Services;

public class EmergencyAlertService : IEmergencyAlertService
{
    private readonly ApplicationDbContext _db;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EmergencyAlertService> _logger;

    public EmergencyAlertService(
        ApplicationDbContext db,
        IEventPublisher eventPublisher,
        ILogger<EmergencyAlertService> logger)
    {
        _db = db;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<List<ActiveEmergencyAlertDto>> GetActiveAlertsAsync(CancellationToken ct = default)
    {
        var alerts = await _db.EmergencyAlerts
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
            .Include(a => a.Mission)
            .Where(a => a.Status == "Active")
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(ct);

        return alerts.Select(MapToActiveDto).ToList();
    }

    public async Task<PaginatedAlertsResponse> GetAlertHistoryAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.EmergencyAlerts
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.ReviewedByUser)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var trimmedStatus = status.Trim().ToLower();
            query = query.Where(a => a.Status.ToLower() == trimmedStatus);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new EmergencyAlertHistoryDto
            {
                Id = a.Id,
                AssetCode = a.Asset != null ? a.Asset.AssetCode : (a.Anomaly != null && a.Anomaly.TowerId != null ? a.Anomaly.TowerId : "N/A"),
                AlertType = a.Anomaly != null && a.Anomaly.Category != null ? a.Anomaly.Category.CategoryName : "Critical Hazard",
                Priority = a.Priority,
                Status = a.Status,
                TriggeredAt = a.TriggeredAt,
                ResolvedAt = a.ResolvedAt,
                ReviewedBy = a.ReviewedByUser != null ? (!string.IsNullOrWhiteSpace(a.ReviewedByUser.FullName) ? a.ReviewedByUser.FullName : a.ReviewedByUser.Email) : null,
                Notes = a.Notes
            })
            .ToListAsync(ct);

        return new PaginatedAlertsResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    public async Task<EmergencyAlertDetailDto?> GetAlertByIdAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await _db.EmergencyAlerts
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.ReviewedByUser)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
            .Include(a => a.AlertEscalations)
                .ThenInclude(e => e.EscalatedByUser)
            .Include(a => a.AlertEscalations)
                .ThenInclude(e => e.EscalatedToUser)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (alert == null) return null;

        return MapToDetailDto(alert);
    }

    public async Task<bool> ReviewAlertAsync(Guid id, Guid reviewerId, ReviewEmergencyAlertRequest request, CancellationToken ct = default)
    {
        var alert = await _db.EmergencyAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert == null) return false;

        var normalizedStatus = string.Equals(request.Status, "Dismissed", StringComparison.OrdinalIgnoreCase)
            ? "Dismissed"
            : "Confirmed";

        alert.Status = normalizedStatus;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ReviewedByUserId = reviewerId;
        alert.Notes = request.Notes;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("EmergencyAlert {AlertId} reviewed by {UserId}: Status = {Status}", id, reviewerId, normalizedStatus);
        return true;
    }

    public async Task<AlertEscalationDto> EscalateAlertAsync(Guid id, Guid currentUserId, EscalateEmergencyAlertRequest request, CancellationToken ct = default)
    {
        var alert = await _db.EmergencyAlerts
            .Include(a => a.Asset)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (alert == null)
            throw new KeyNotFoundException($"Emergency alert with ID {id} was not found.");

        alert.Status = "Escalated";
        alert.UpdatedAt = DateTime.UtcNow;

        var escalation = new AlertEscalation
        {
            Id = Guid.NewGuid(),
            AlertId = id,
            EscalatedBy = currentUserId,
            EscalatedTo = request.EscalatedToUserId,
            Reason = request.Reason,
            EscalatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.AlertEscalations.Add(escalation);
        await _db.SaveChangesAsync(ct);

        var escalater = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
        var manager = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.EscalatedToUserId, ct);

        var alertTypeName = alert.Anomaly?.Category?.CategoryName ?? "Sự cố khẩn cấp";
        var assetCode = alert.Asset?.AssetCode ?? alert.Anomaly?.TowerId ?? "vị trí kiểm tra";

        try
        {
            await _eventPublisher.PublishAsync(new NotificationPushEvent
            {
                NotificationId = Guid.NewGuid(),
                UserId = request.EscalatedToUserId,
                Type = "EmergencyAlert",
                ReferenceType = "EmergencyAlert",
                ReferenceId = alert.Id,
                Title = $"⚡ Cảnh báo khẩn cấp được chuyển cấp: {alertTypeName}",
                Body = $"Analyst {(escalater?.FullName ?? "hệ thống")} đã chuyển cấp cảnh báo sự cố tại {assetCode} đến bạn. Lý do: {request.Reason}",
                SentAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish NotificationPushEvent for alert escalation {AlertId}", id);
        }

        return new AlertEscalationDto
        {
            Id = escalation.Id,
            EscalatedBy = escalation.EscalatedBy,
            EscalatedByName = !string.IsNullOrWhiteSpace(escalater?.FullName) ? escalater.FullName : (escalater?.Email ?? "Analyst"),
            EscalatedTo = escalation.EscalatedTo,
            EscalatedToName = !string.IsNullOrWhiteSpace(manager?.FullName) ? manager.FullName : (manager?.Email ?? "Manager"),
            Reason = escalation.Reason,
            EscalatedAt = escalation.EscalatedAt
        };
    }

    public async Task<EmergencyAlertDetailDto> TriggerAlertAsync(TriggerEmergencyAlertRequest request, CancellationToken ct = default)
    {
        var missionId = request.MissionId ?? Guid.Empty;
        if (missionId == Guid.Empty)
        {
            var latestMission = await _db.Missions.OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync(ct);
            if (latestMission != null)
            {
                missionId = latestMission.Id;
            }
        }

        Guid anomalyId;
        if (request.AnomalyId.HasValue && request.AnomalyId.Value != Guid.Empty)
        {
            anomalyId = request.AnomalyId.Value;
        }
        else
        {
            var defaultCategory = await _db.DefectCategories.FirstOrDefaultAsync(c => c.IsEmergencyClass, ct)
                ?? await _db.DefectCategories.FirstOrDefaultAsync(ct);

            var newAnomaly = new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                AssetId = request.AssetId,
                CategoryId = defaultCategory?.Id ?? 1,
                BoundingBox = request.BoundingBox ?? "{\"x\": 100, \"y\": 100, \"w\": 150, \"h\": 150}",
                ImageUrl = request.MediaUrl ?? "/storage/alerts/sample_emergency.jpg",
                ConfidenceScore = request.ConfidenceScore ?? 0.94,
                TowerId = request.TowerCode ?? "TOW-N1-05",
                ValidationStatus = "Unvalidated",
                AiSource = "EdgeAI",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.DetectedAnomalies.Add(newAnomaly);
            anomalyId = newAnomaly.Id;
        }

        var alert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            AnomalyId = anomalyId,
            AssetId = request.AssetId,
            MissionId = missionId,
            Status = "Active",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Critical" : request.Priority,
            DeliveryLatencySeconds = request.DeliveryLatencySeconds > 0 ? request.DeliveryLatencySeconds : 3,
            TriggeredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.EmergencyAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EmergencyAlert {AlertId} triggered successfully by Edge AI / Drone", alert.Id);

        // Fail-Safe: Record safety warning communication log on the active flight mission
        if (missionId != Guid.Empty)
        {
            var activeMission = await _db.Missions.FirstOrDefaultAsync(m => m.Id == missionId, ct);
            if (activeMission != null)
            {
                var commLog = new MissionCommunicationLog
                {
                    Id = Guid.NewGuid(),
                    MissionId = activeMission.Id,
                    SenderId = Guid.Empty,
                    SenderName = "Edge AI Safety Guard",
                    SenderRole = "SYSTEM",
                    Type = "EMERGENCY_ALERT",
                    Content = $"[CẢNH BÁO NGUY HIỂM] {request.AlertType ?? "Sự cố khẩn cấp"} tại {request.TowerCode ?? "vùng bay"}. Khuyến nghị phi công rà soát an toàn hoặc kích hoạt chế độ RTH quay về.",
                    CreatedAt = DateTime.UtcNow
                };
                _db.MissionCommunicationLogs.Add(commLog);
                await _db.SaveChangesAsync(ct);
            }
        }

        // Notify all online Analysts & Managers
        try
        {
            var targetUsers = await _db.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.Role != null && (ur.Role.RoleName == "Analyst" || ur.Role.RoleName == "Manager" || ur.Role.RoleName == "SystemAdmin"))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

            var title = $"🚨 CẢNH BÁO KHẨN CẤP: {request.AlertType ?? "Sự cố nghiêm trọng"}";
            var body = $"Edge AI phát hiện sự cố khẩn cấp tại {request.TowerCode ?? "khu vực bay"}. Độ trễ truyền tin {alert.DeliveryLatencySeconds}s. Yêu cầu kiểm tra ngay!";

            foreach (var userId in targetUsers)
            {
                await _eventPublisher.PublishAsync(new NotificationPushEvent
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Type = "EmergencyAlert",
                    ReferenceType = "EmergencyAlert",
                    ReferenceId = alert.Id,
                    Title = title,
                    Body = body,
                    SentAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish broadcast notifications for new emergency alert {AlertId}", alert.Id);
        }

        var detail = await GetAlertByIdAsync(alert.Id, ct);
        return detail!;
    }

    private static ActiveEmergencyAlertDto MapToActiveDto(EmergencyAlert a)
    {
        return new ActiveEmergencyAlertDto
        {
            Id = a.Id,
            AnomalyId = a.AnomalyId,
            AssetId = a.AssetId,
            AssetCode = a.Asset != null ? a.Asset.AssetCode : (a.Anomaly?.TowerId ?? "N/A"),
            AlertType = a.Anomaly?.Category?.CategoryName ?? "Wildfire / Flashover",
            CategoryName = a.Anomaly?.Category?.CategoryName ?? "Insulator Flashover",
            Priority = string.IsNullOrWhiteSpace(a.Priority) ? "Critical" : a.Priority,
            Status = a.Status,
            DeliveryLatencySeconds = a.DeliveryLatencySeconds,
            TriggeredAt = a.TriggeredAt,
            Location = new AlertLocationDto
            {
                Latitude = a.Asset?.Location?.Y,
                Longitude = a.Asset?.Location?.X,
                TowerCode = a.Anomaly?.TowerId ?? a.Asset?.AssetCode
            },
            Evidence = new AlertEvidenceDto
            {
                MediaUrl = a.Anomaly?.ImageUrl ?? a.Anomaly?.CropUrl,
                BoundingBox = a.Anomaly?.BoundingBox,
                ConfidenceScore = a.Anomaly?.ConfidenceScore
            }
        };
    }

    private static EmergencyAlertDetailDto MapToDetailDto(EmergencyAlert a)
    {
        return new EmergencyAlertDetailDto
        {
            Id = a.Id,
            AnomalyId = a.AnomalyId,
            AssetId = a.AssetId,
            AssetCode = a.Asset != null ? a.Asset.AssetCode : (a.Anomaly?.TowerId ?? "N/A"),
            MissionId = a.MissionId,
            AlertType = a.Anomaly?.Category?.CategoryName ?? "Wildfire / Flashover",
            CategoryName = a.Anomaly?.Category?.CategoryName ?? "Insulator Flashover",
            Priority = string.IsNullOrWhiteSpace(a.Priority) ? "Critical" : a.Priority,
            Status = a.Status,
            DeliveryLatencySeconds = a.DeliveryLatencySeconds,
            TriggeredAt = a.TriggeredAt,
            ReceivedAt = a.ReceivedAt,
            ResolvedAt = a.ResolvedAt,
            ReviewedByUserId = a.ReviewedByUserId,
            ReviewedByName = a.ReviewedByUser != null ? (!string.IsNullOrWhiteSpace(a.ReviewedByUser.FullName) ? a.ReviewedByUser.FullName : a.ReviewedByUser.Email) : null,
            Notes = a.Notes,
            Location = new AlertLocationDto
            {
                Latitude = a.Asset?.Location?.Y,
                Longitude = a.Asset?.Location?.X,
                TowerCode = a.Anomaly?.TowerId ?? a.Asset?.AssetCode
            },
            Evidence = new AlertEvidenceDto
            {
                MediaUrl = a.Anomaly?.ImageUrl ?? a.Anomaly?.CropUrl,
                BoundingBox = a.Anomaly?.BoundingBox,
                ConfidenceScore = a.Anomaly?.ConfidenceScore
            },
            Escalations = a.AlertEscalations.Select(e => new AlertEscalationDto
            {
                Id = e.Id,
                EscalatedBy = e.EscalatedBy,
                EscalatedByName = e.EscalatedByUser != null ? (!string.IsNullOrWhiteSpace(e.EscalatedByUser.FullName) ? e.EscalatedByUser.FullName : e.EscalatedByUser.Email) : "Analyst",
                EscalatedTo = e.EscalatedTo,
                EscalatedToName = e.EscalatedToUser != null ? (!string.IsNullOrWhiteSpace(e.EscalatedToUser.FullName) ? e.EscalatedToUser.FullName : e.EscalatedToUser.Email) : "Manager",
                Reason = e.Reason,
                EscalatedAt = e.EscalatedAt
            }).OrderByDescending(e => e.EscalatedAt).ToList()
        };
    }
}
