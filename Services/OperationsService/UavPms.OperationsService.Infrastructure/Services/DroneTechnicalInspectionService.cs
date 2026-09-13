using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class DroneTechnicalInspectionService : IDroneTechnicalInspectionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;

    public DroneTechnicalInspectionService(ApplicationDbContext db, ICurrentUserServices current)
    {
        _db = db;
        _current = current;
    }

    public async Task<DroneTechnicalInspection> SubmitInspectionAsync(DroneInspectionSubmitRequest request, CancellationToken ct)
    {
        await RequireTechnicianOrManager(ct);

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == request.DroneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", request.DroneId);

        if (request.Metrics == null || request.Metrics.Count == 0)
            throw new BusinessRuleException("METRICS_REQUIRED", "At least one technical inspection metric must be provided.");

        var inspection = new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            TechnicianUserId = _current.UserId,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            PolicyVersion = request.PolicyVersion ?? "v2.0",
            Notes = request.Notes,
            SourceType = "manual",
            SourceVersion = "v2.0"
        };

        var metrics = new List<DroneTechnicalMetric>();
        var anyCriticalFailed = false;
        var anyFailed = false;

        foreach (var m in request.Metrics)
        {
            var passed = m.Passed ?? (m.BoolValue ?? (m.NumericValue.HasValue ? m.NumericValue.Value >= 0 : true));
            if (!passed)
            {
                anyFailed = true;
                if (m.Critical) anyCriticalFailed = true;
            }

            metrics.Add(new DroneTechnicalMetric
            {
                Inspection = inspection,
                MetricCode = m.MetricCode,
                Subsystem = m.Subsystem,
                NumericValue = m.NumericValue,
                BoolValue = m.BoolValue,
                ValueText = m.ValueText,
                Unit = m.Unit,
                Passed = passed,
                Critical = m.Critical,
                Severity = m.Severity,
                IsRequired = m.IsRequired
            });
        }

        inspection.Metrics = metrics;

        if (anyCriticalFailed)
        {
            inspection.Status = DroneTechnicalInspectionStatus.Failed;
            inspection.Health = TechnicalHealth.Critical;
        }
        else if (anyFailed)
        {
            inspection.Status = DroneTechnicalInspectionStatus.Passed;
            inspection.Health = TechnicalHealth.Warning;
        }
        else
        {
            inspection.Status = DroneTechnicalInspectionStatus.Passed;
            inspection.Health = TechnicalHealth.Healthy;
        }

        inspection.RawSnapshot = JsonSerializer.Serialize(new
        {
            totalMetrics = metrics.Count,
            criticalFailures = anyCriticalFailed,
            warnings = anyFailed && !anyCriticalFailed,
            evaluatedAt = DateTime.UtcNow
        });

        _db.DroneTechnicalInspections.Add(inspection);

        // Update drone's technical health state
        drone.TechnicalHealth = inspection.Health;
        drone.LastTechnicalInspectionId = inspection.Id;
        drone.TechnicalHealthUpdatedAt = DateTime.UtcNow;

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = "DroneTechnicalInspections",
            RecordId = inspection.Id,
            ActionType = "INSPECTION_SUBMITTED",
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new
            {
                droneId = drone.Id,
                status = inspection.Status.ToString(),
                health = inspection.Health.ToString()
            }),
            IpAddress = _current.IpAddress ?? "",
            UserAgent = _current.UserAgent ?? ""
        });

        await _db.SaveChangesAsync(ct);
        return inspection;
    }

    public async Task<DroneTechnicalInspection?> GetLatestInspectionAsync(Guid droneId, CancellationToken ct)
    {
        if (!_current.IsAuthenticated)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (!await _db.Uavs.AnyAsync(x => x.Id == droneId && !x.IsDeleted, ct))
            throw new NotFoundException("Drone", droneId);

        return await _db.DroneTechnicalInspections
            .Include(x => x.Metrics)
            .Include(x => x.Technician)
            .Where(x => x.DroneId == droneId)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task RequireTechnicianOrManager(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isAllowed = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.MaintenanceTechnician, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
            throw new ForbiddenException("TECHNICIAN_OR_MANAGER_ROLE_REQUIRED");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct);
        if (user == null || (user.Status != "Active" && user.Status != "Enabled"))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");
    }
}
