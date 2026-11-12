using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Schedules;
using UavPms.OperationsService.Application.Features.Schedules.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class InspectionScheduleService : IInspectionScheduleService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _currentUser;
    private readonly GeographicAccessFilter _accessFilter;

    public InspectionScheduleService(
        ApplicationDbContext db,
        ICurrentUserServices currentUser,
        GeographicAccessFilter accessFilter)
    {
        _db = db;
        _currentUser = currentUser;
        _accessFilter = accessFilter;
    }

    public async Task<PaginatedInspectionSchedulesResponse> GetPagedAsync(
        int page,
        int pageSize,
        Guid? regionId,
        bool? isActive,
        string? search,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.InspectionSchedules
            .AsNoTracking()
            .Include(s => s.Region)
            .AsQueryable();

        var accessibleRegionIds = _accessFilter.ApplyToRegions(_db.Regions.AsNoTracking()).Select(r => r.Id);
        query = query.Where(s => accessibleRegionIds.Contains(s.RegionId));

        if (regionId.HasValue && regionId.Value != Guid.Empty)
        {
            query = query.Where(s => s.RegionId == regionId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                s.RecurrenceRule.ToLower().Contains(term) ||
                (s.Region != null && s.Region.Name.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync(ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToDto(s, null))
            .ToListAsync(ct);

        return new PaginatedInspectionSchedulesResponse(
            items,
            new PaginationMetaData(page, pageSize, totalItems, totalPages));
    }

    public async Task<InspectionScheduleDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var schedule = await _db.InspectionSchedules
            .AsNoTracking()
            .Include(s => s.Region)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(InspectionSchedule), id);

        var hasAccess = await _accessFilter.ApplyToRegions(_db.Regions.AsNoTracking())
            .AnyAsync(r => r.Id == schedule.RegionId, ct);
        if (!hasAccess)
        {
            throw new ForbiddenException("SCHEDULE_ACCESS_DENIED: You do not have access to this inspection schedule.");
        }

        return ToDto(schedule);
    }

    public async Task<InspectionScheduleDto> CreateAsync(
        Guid regionId,
        string name,
        string recurrenceRule,
        TimeOnly plannedTime,
        bool isActive,
        CancellationToken ct)
    {
        var region = await ValidateAndEnsureWriteAccessAsync(regionId, name, recurrenceRule, ct);

        var schedule = new InspectionSchedule
        {
            Id = Guid.NewGuid(),
            RegionId = region.Id,
            Name = name.Trim(),
            RecurrenceRule = recurrenceRule.Trim(),
            PlannedTime = plannedTime,
            IsActive = isActive,
            CreatedBy = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.InspectionSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return ToDto(schedule, region.Name);
    }

    public async Task<InspectionScheduleDto> UpdateAsync(
        Guid id,
        Guid regionId,
        string name,
        string recurrenceRule,
        TimeOnly plannedTime,
        bool isActive,
        CancellationToken ct)
    {
        var schedule = await _db.InspectionSchedules
            .Include(s => s.Region)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(InspectionSchedule), id);

        await EnsureRegionWriteAccessAsync(schedule.RegionId, ct);
        var region = await ValidateAndEnsureWriteAccessAsync(regionId, name, recurrenceRule, ct);

        schedule.RegionId = region.Id;
        schedule.Name = name.Trim();
        schedule.RecurrenceRule = recurrenceRule.Trim();
        schedule.PlannedTime = plannedTime;
        schedule.IsActive = isActive;
        schedule.UpdatedBy = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ToDto(schedule, region.Name);
    }

    public async Task<InspectionScheduleDto> ToggleActiveAsync(Guid id, bool? isActive, CancellationToken ct)
    {
        var schedule = await _db.InspectionSchedules
            .Include(s => s.Region)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(InspectionSchedule), id);

        await EnsureRegionWriteAccessAsync(schedule.RegionId, ct);

        schedule.IsActive = isActive ?? !schedule.IsActive;
        schedule.UpdatedBy = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(schedule);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var schedule = await _db.InspectionSchedules
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(InspectionSchedule), id);

        await EnsureRegionWriteAccessAsync(schedule.RegionId, ct);

        schedule.IsDeleted = true;
        schedule.DeletedAt = DateTime.UtcNow;
        schedule.UpdatedBy = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<TriggerScheduleResultDto> TriggerAsync(Guid id, CancellationToken ct)
    {
        var schedule = await _db.InspectionSchedules
            .Include(s => s.Region)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(InspectionSchedule), id);

        await EnsureRegionWriteAccessAsync(schedule.RegionId, ct);

        var actorId = _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : (schedule.CreatedBy ?? Guid.Empty);

        return await CreateAssessmentForScheduleAsync(schedule, actorId, DateTime.UtcNow, ct);
    }

    internal async Task<TriggerScheduleResultDto?> TriggerFromRunnerAsync(
        InspectionSchedule schedule,
        DateTime utcNow,
        CancellationToken ct)
    {
        var managerId = schedule.CreatedBy ?? await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null && (ur.Role.RoleName == UserRoles.Manager || ur.Role.RoleName == UserRoles.SystemAdmin))
            .Select(ur => ur.UserId)
            .FirstOrDefaultAsync(ct);

        if (managerId == Guid.Empty)
            return null;

        return await CreateAssessmentForScheduleAsync(schedule, managerId, utcNow, ct);
    }

    private async Task<TriggerScheduleResultDto> CreateAssessmentForScheduleAsync(
        InspectionSchedule schedule,
        Guid managerId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var regionTowerIds = await _db.Towers
            .AsNoTracking()
            .Where(t => t.RegionId == schedule.RegionId && !t.IsDeleted)
            .OrderBy(t => t.TowerCode)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (regionTowerIds.Count == 0)
        {
            throw new BusinessRuleException("SCHEDULE_REGION_HAS_NO_TOWERS",
                $"Region '{schedule.RegionId}' has no active towers to schedule an inspection.");
        }

        if (managerId == Guid.Empty)
        {
            throw new BusinessRuleException("MANAGER_REQUIRED", "A valid manager user is required to trigger an inspection schedule.");
        }

        var plannedStart = utcNow.Date.Add(schedule.PlannedTime.ToTimeSpan());
        if (plannedStart <= utcNow)
        {
            plannedStart = utcNow.AddMinutes(30);
        }
        var plannedEnd = plannedStart.AddHours(4);

        var assessment = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            RegionId = schedule.RegionId,
            Status = PreMissionAssessmentStatus.Draft,
            SiteFeasibilityStatus = ReadinessCheckStatus.Pending,
            OverallTechnicalHealth = TechnicalHealth.Unknown,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            ValidUntil = plannedStart.AddDays(1),
            EvaluationPolicyVersion = "v2.2",
            Findings = $"{{\"scheduleId\":\"{schedule.Id}\",\"scheduleName\":\"{schedule.Name.Replace("\"", "\\\"")}\"}}",
            CreatedBy = managerId,
            CreatedAt = utcNow
        };

        var seq = 1;
        foreach (var towerId in regionTowerIds)
        {
            assessment.Towers.Add(new PreMissionAssessmentTower
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                TowerId = towerId,
                Sequence = seq++,
                CreatedBy = managerId,
                CreatedAt = utcNow
            });
        }

        _db.PreMissionAssessments.Add(assessment);
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = managerId,
            TableName = "InspectionSchedules",
            RecordId = schedule.Id,
            ActionType = "SCHEDULE_TRIGGERED",
            OldValues = "{}",
            NewValues = $"{{\"assessmentId\":\"{assessment.Id}\",\"towerCount\":{regionTowerIds.Count}}}",
            IpAddress = string.Empty,
            UserAgent = nameof(InspectionScheduleRunnerJob),
            CreatedAt = utcNow
        });

        await _db.SaveChangesAsync(ct);

        return new TriggerScheduleResultDto(
            schedule.Id,
            assessment.Id,
            assessment.Status.ToString(),
            regionTowerIds.Count,
            plannedStart,
            plannedEnd,
            utcNow);
    }

    private async Task<Region> ValidateAndEnsureWriteAccessAsync(
        Guid regionId,
        string name,
        string recurrenceRule,
        CancellationToken ct)
    {
        if (regionId == Guid.Empty)
            throw new BusinessRuleException("INVALID_REGION", "RegionId is required.");

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256)
            throw new BusinessRuleException("INVALID_SCHEDULE_NAME", "Schedule name is required and must be at most 256 characters.");

        if (!RecurrenceRuleEvaluator.TryValidate(recurrenceRule, out var rruleError))
            throw new BusinessRuleException("INVALID_RECURRENCE_RULE", rruleError ?? "Invalid recurrence rule.");

        if (recurrenceRule.Trim().Length > 512)
            throw new BusinessRuleException("INVALID_RECURRENCE_RULE", "RecurrenceRule must be at most 512 characters.");

        var region = await _db.Regions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == regionId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Region), regionId);

        await EnsureRegionWriteAccessAsync(regionId, ct);
        return region;
    }

    private async Task EnsureRegionWriteAccessAsync(Guid regionId, CancellationToken ct)
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        if (roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        if (!roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("SCHEDULE_ACCESS_DENIED: Only SystemAdmin or Manager can modify inspection schedules.");

        var hasAccess = await _accessFilter.ApplyToRegions(_db.Regions.AsNoTracking())
            .AnyAsync(r => r.Id == regionId, ct);
        if (!hasAccess)
        {
            throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED: Manager does not have scope for this region.");
        }
    }

    private static InspectionScheduleDto ToDto(InspectionSchedule s, string? regionNameOverride = null) => new()
    {
        Id = s.Id,
        RegionId = s.RegionId,
        RegionName = regionNameOverride ?? s.Region?.Name ?? string.Empty,
        Name = s.Name,
        RecurrenceRule = s.RecurrenceRule,
        PlannedTime = s.PlannedTime,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
        CreatedBy = s.CreatedBy,
        UpdatedAt = s.UpdatedAt
    };
}

