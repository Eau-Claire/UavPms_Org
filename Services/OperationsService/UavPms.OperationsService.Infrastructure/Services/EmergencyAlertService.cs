using UavPms.OperationsService.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Common.Exceptions;
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
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Tower)
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Media)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
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
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Tower)
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Media)
            .Include(a => a.ReviewedBy)
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
                TowerCode = a.Anomaly != null && a.Anomaly.Tower != null ? a.Anomaly.Tower.TowerCode : "N/A",
                AlertType = a.Anomaly != null && a.Anomaly.Category != null ? a.Anomaly.Category.CategoryName : "Critical Hazard",
                Priority = a.Priority,
                Status = a.Status,
                TriggeredAt = a.TriggeredAt,
                ResolvedAt = a.ResolvedAt,
                ReviewedBy = a.ReviewedBy != null ? (!string.IsNullOrWhiteSpace(a.ReviewedBy.FullName) ? a.ReviewedBy.FullName : a.ReviewedBy.Email) : null,
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
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Tower)
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Media)
            .Include(a => a.ReviewedBy)
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
        alert.ReviewedById = reviewerId;
        alert.Notes = request.Notes;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("EmergencyAlert {AlertId} reviewed by {UserId}: Status = {Status}", id, reviewerId, normalizedStatus);
        return true;
    }

    public async Task<AlertEscalationDto> EscalateAlertAsync(Guid id, Guid currentUserId, EscalateEmergencyAlertRequest request, CancellationToken ct = default)
    {
        var alert = await _db.EmergencyAlerts
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Tower)
            .Include(a => a.Anomaly!)
                .ThenInclude(an => an.Media)
            .Include(a => a.Anomaly)
                .ThenInclude(an => an!.Category)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (alert == null)
            throw new KeyNotFoundException($"Emergency alert with ID {id} was not found.");

        // CK_AlertEscalations_DifferentUsers
        if (request.EscalatedToUserId == currentUserId)
            throw new BusinessRuleException("INVALID_ESCALATION_TARGET", "An alert cannot be escalated to the user escalating it.");

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
        var towerCode = alert.Anomaly?.Tower?.TowerCode ?? "vị trí kiểm tra";

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
                Body = $"Analyst {(escalater?.FullName ?? "hệ thống")} đã chuyển cấp cảnh báo sự cố tại {towerCode} đến bạn. Lý do: {request.Reason}",
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
            if (missionId == Guid.Empty)
                throw new InvalidOperationException("An edge alert without an existing anomaly requires a mission to attach its media to.");

            var defaultCategory = await _db.DefectCategories.FirstOrDefaultAsync(c => c.IsEmergencyClass, ct)
                ?? await _db.DefectCategories.FirstOrDefaultAsync(ct);
            var mission = await _db.Missions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == missionId, ct);
            var towerId = request.TowerId;
            if (!towerId.HasValue && !string.IsNullOrWhiteSpace(request.TowerCode))
            {
                towerId = await _db.Towers.Where(t => t.TowerCode == request.TowerCode)
                    .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            }

            var now = DateTime.UtcNow;
            var media = new InspectionMedia
            {
                Id = Guid.NewGuid(),
                MissionId = missionId,
                TowerId = towerId,
                MediaType = "Image",
                FileUrl = request.MediaUrl ?? "/storage/alerts/sample_emergency.jpg",
                ValidationStatus = "Pending",
                CapturedAt = now
            };
            var run = new AIAnalysisRun
            {
                Id = Guid.NewGuid(),
                MediaId = media.Id,
                ExecutionLocation = AIAnalysisRun.Edge,
                UavId = mission?.UavId,
                AnalysisType = AnalysisType.DefectDetection,
                ModelName = "EdgeAI",
                Status = AIAnalysisStatus.Completed,
                StartedAt = now,
                CompletedAt = now
            };
            var newAnomaly = new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                MediaId = media.Id,
                AnalysisRunId = run.Id,
                TowerId = towerId,
                CategoryId = defaultCategory?.Id ?? 1,
                BoundingBox = request.BoundingBox ?? "{\"x\": 100, \"y\": 100, \"w\": 150, \"h\": 150}",
                ImageUrl = media.FileUrl,
                ConfidenceScore = request.ConfidenceScore ?? 0.94,
                ValidationStatus = "Pending"
            };

            _db.InspectionMedia.Add(media);
            _db.AIAnalysisRuns.Add(run);
            _db.DetectedAnomalies.Add(newAnomaly);
            anomalyId = newAnomaly.Id;
        }

        // UX_EmergencyAlerts_ActivePerAnomaly: at most one Active alert per anomaly.
        var activeAlertId = await _db.EmergencyAlerts
            .Where(a => a.AnomalyId == anomalyId && a.Status == "Active")
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
        if (activeAlertId.HasValue)
            throw new BusinessRuleException("ALERT_ALREADY_ACTIVE",
                $"Anomaly {anomalyId} already has an active emergency alert ({activeAlertId}).");

        var alert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            AnomalyId = anomalyId,
            Status = "Active",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Critical" : request.Priority,
            TriggeredAt = DateTime.UtcNow,
            ReceivedAt = request.DeliveryLatencySeconds > 0 ? DateTime.UtcNow.AddSeconds(request.DeliveryLatencySeconds) : null
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
                    SenderId = null,
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
            var body = $"Edge AI phát hiện sự cố khẩn cấp tại {request.TowerCode ?? "khu vực bay"}. Độ trễ truyền tin {request.DeliveryLatencySeconds}s. Yêu cầu kiểm tra ngay!";

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
            TowerId = a.Anomaly?.TowerId,
            TowerCode = a.Anomaly?.Tower?.TowerCode ?? "N/A",
            AlertType = a.Anomaly?.Category?.CategoryName ?? "Wildfire / Flashover",
            CategoryName = a.Anomaly?.Category?.CategoryName ?? "Insulator Flashover",
            Priority = string.IsNullOrWhiteSpace(a.Priority) ? "Critical" : a.Priority,
            Status = a.Status,
            DeliveryLatencySeconds = a.ReceivedAt.HasValue ? Math.Max(0, (int)(a.ReceivedAt.Value - a.TriggeredAt).TotalSeconds) : 0,
            TriggeredAt = a.TriggeredAt,
            Location = new AlertLocationDto
            {
                Latitude = a.Anomaly?.Tower?.Geom?.Coordinate.Y ?? a.Anomaly?.Location?.Y,
                Longitude = a.Anomaly?.Tower?.Geom?.Coordinate.X ?? a.Anomaly?.Location?.X,
                TowerCode = a.Anomaly?.Tower?.TowerCode
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
            TowerId = a.Anomaly?.TowerId,
            TowerCode = a.Anomaly?.Tower?.TowerCode ?? "N/A",
            MissionId = a.Anomaly?.Media?.MissionId ?? Guid.Empty,
            AlertType = a.Anomaly?.Category?.CategoryName ?? "Wildfire / Flashover",
            CategoryName = a.Anomaly?.Category?.CategoryName ?? "Insulator Flashover",
            Priority = string.IsNullOrWhiteSpace(a.Priority) ? "Critical" : a.Priority,
            Status = a.Status,
            DeliveryLatencySeconds = a.ReceivedAt.HasValue ? Math.Max(0, (int)(a.ReceivedAt.Value - a.TriggeredAt).TotalSeconds) : 0,
            TriggeredAt = a.TriggeredAt,
            ReceivedAt = a.ReceivedAt,
            ResolvedAt = a.ResolvedAt,
            ReviewedByUserId = a.ReviewedById,
            ReviewedByName = a.ReviewedBy != null ? (!string.IsNullOrWhiteSpace(a.ReviewedBy.FullName) ? a.ReviewedBy.FullName : a.ReviewedBy.Email) : null,
            Notes = a.Notes,
            Location = new AlertLocationDto
            {
                Latitude = a.Anomaly?.Tower?.Geom?.Coordinate.Y ?? a.Anomaly?.Location?.Y,
                Longitude = a.Anomaly?.Tower?.Geom?.Coordinate.X ?? a.Anomaly?.Location?.X,
                TowerCode = a.Anomaly?.Tower?.TowerCode
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
