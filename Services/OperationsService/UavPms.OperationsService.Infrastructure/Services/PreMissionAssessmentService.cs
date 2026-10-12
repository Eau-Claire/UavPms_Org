using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class PreMissionAssessmentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;

    public PreMissionAssessmentService(ApplicationDbContext db, ICurrentUserServices current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken ct)
    {
        return await CreateAsync(regionId, plannedStart, plannedEnd, assetIds, null, null, ct);
    }

    public async Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> assetIds,
        string? boundaryWkt,
        string? idempotencyKey,
        CancellationToken ct)
    {
        await RequireManager(ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.PreMissionAssessments
                .Include(x => x.Assets)
                .Include(x => x.PersonnelCandidates)
                .Include(x => x.DroneCandidates)
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
            if (existing != null) return existing;
        }

        if (plannedEnd <= plannedStart || plannedEnd <= DateTime.UtcNow)
            throw new BusinessRuleException("INVALID_PLANNED_TIME");

        if (assetIds.Count == 0 || assetIds.Count != assetIds.Distinct().Count())
            throw new BusinessRuleException("INVALID_PROPOSED_SCOPE");

        var region = await _db.Regions.SingleOrDefaultAsync(x => x.Id == regionId, ct)
            ?? throw new NotFoundException("Region", regionId);
        if (region.IsDeleted)
            throw new BusinessRuleException("REGION_INACTIVE");

        if (!_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) &&
            !await _db.UserGeographicScopes.AnyAsync(x => x.UserId == _current.UserId && (x.RegionId == regionId || x.RegionId == null), ct))
            throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED");

        var canonicalAssets = await _db.Assets
            .Where(x => assetIds.Contains(x.Id) &&
                        (x.Status == "Active" || x.Status == "Operational") &&
                        x.Tower!.TransmissionLine!.Substation!.RegionAssetId == regionId)
            .ToListAsync(ct);

        if (canonicalAssets.Count != assetIds.Count)
            throw new ForbiddenException("ASSET_OUTSIDE_MANAGEMENT_SCOPE");

        Geometry? boundary = null;
        if (!string.IsNullOrWhiteSpace(boundaryWkt))
        {
            boundary = SiteFeasibilityPolicy.ParseBoundary(boundaryWkt);
        }

        var siteResult = SiteFeasibilityPolicy.Evaluate(region, canonicalAssets, boundary, assetIds);
        if (!siteResult.IsValid)
            throw new BusinessRuleException(siteResult.ErrorMessage ?? "SITE_FEASIBILITY_FAILED");

        var assessment = new PreMissionAssessment
        {
            ManagerId = _current.UserId,
            RegionId = regionId,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            ProposedBoundary = boundary,
            SiteFeasibilityStatus = siteResult.Status,
            Status = PreMissionAssessmentStatus.Evaluating,
            Findings = siteResult.FindingsJson,
            EvaluationPolicyVersion = "v2.0",
            IdempotencyKey = idempotencyKey
        };

        assessment.Assets = assetIds
            .Select((id, i) => new PreMissionAssessmentAsset
            {
                Assessment = assessment,
                AssetId = id,
                Sequence = i + 1
            })
            .ToList();

        _db.PreMissionAssessments.Add(assessment);
        Audit(assessment.Id, "ASSESSMENT_CREATED");
        try
        {
            await SaveWithConcurrency(ct);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.PreMissionAssessments
                .AsNoTracking()
                .Include(x => x.Assets)
                .Include(x => x.PersonnelCandidates)
                .Include(x => x.DroneCandidates)
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

            if (existing != null)
                return existing;

            throw new BusinessRuleException("IDEMPOTENCY_CONFLICT", "An assessment with the same idempotency key is currently being processed.");
        }

        return await EvaluateAsync(assessment.Id, ct);
    }

    public async Task<PreMissionAssessment> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireManager(ct);

        var assessment = await _db.PreMissionAssessments
            .Include(x => x.Assets).ThenInclude(a => a.Asset)
            .Include(x => x.PersonnelCandidates).ThenInclude(p => p.User)
            .Include(x => x.DroneCandidates).ThenInclude(d => d.Drone)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PreMissionAssessment", id);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (AssessmentExpiryPolicy.CheckAndApplyExpiry(assessment))
        {
            Audit(assessment.Id, "ASSESSMENT_EXPIRED");
            await SaveWithConcurrency(ct);
        }

        return assessment;
    }

    public async Task<IReadOnlyList<PreMissionAssessment>> ListAsync(CancellationToken ct)
    {
        return await ListAsync(null, ct);
    }

    public async Task<IReadOnlyList<PreMissionAssessment>> ListAsync(string? status, CancellationToken ct)
    {
        await RequireManager(ct);

        var isGlobal = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
        List<Guid?>? userScopes = null;
        if (!isGlobal)
        {
            userScopes = await _db.UserGeographicScopes
                .Where(x => x.UserId == _current.UserId)
                .Select(x => x.RegionId)
                .ToListAsync(ct);
        }

        var hasGlobalScope = userScopes != null && userScopes.Contains(null);

        var query = _db.PreMissionAssessments
            .Include(x => x.Assets)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .Where(x => isGlobal ||
                        hasGlobalScope ||
                        x.ManagerId == _current.UserId ||
                        (userScopes != null && userScopes.Contains(x.RegionId)));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsedStatus = NormalizeAssessmentStatusFilter(status);
            if (parsedStatus.HasValue)
            {
                query = query.Where(x => x.Status == parsedStatus.Value);
            }
            else
            {
                query = query.Where(x => false);
            }
        }

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var expiredAny = false;
        foreach (var a in list)
        {
            if (AssessmentExpiryPolicy.CheckAndApplyExpiry(a))
                expiredAny = true;
        }

        if (expiredAny)
            await _db.SaveChangesAsync(ct);

        return list;
    }

    public static PreMissionAssessmentStatus? NormalizeAssessmentStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var clean = status.Trim().Replace("-", "_").ToUpperInvariant();
        return clean switch
        {
            "DRAFT" => PreMissionAssessmentStatus.Draft,
            "EVALUATING" => PreMissionAssessmentStatus.Evaluating,
            "READY" => PreMissionAssessmentStatus.Ready,
            "NOT_READY" or "NOTREADY" or "INCOMPLETE" => PreMissionAssessmentStatus.NotReady,
            "EXPIRED" => PreMissionAssessmentStatus.Expired,
            "COMPLETED" or "CONSUMED" => PreMissionAssessmentStatus.Completed,
            "CANCELLED" or "CANCELED" => PreMissionAssessmentStatus.Cancelled,
            _ => Enum.TryParse<PreMissionAssessmentStatus>(clean, true, out var parsed) ? parsed : null
        };
    }

    public async Task<PreMissionAssessment> EvaluateAsync(Guid id, CancellationToken ct)
    {
        await RequireManager(ct);

        var assessment = await _db.PreMissionAssessments
            .Include(x => x.Assets)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PreMissionAssessment", id);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (assessment.Status == PreMissionAssessmentStatus.Completed)
            throw new BusinessRuleException("ASSESSMENT_ALREADY_COMPLETED", "Đánh giá tiền nhiệm vụ đã hoàn thành tạo nhiệm vụ bay.");

        if (assessment.Status == PreMissionAssessmentStatus.Cancelled)
            throw new BusinessRuleException("ASSESSMENT_CANCELLED", "Bản đánh giá đã bị hủy, không thể đánh giá lại.");

        if (assessment.Status == PreMissionAssessmentStatus.Expired ||
            assessment.PlannedEnd <= DateTime.UtcNow ||
            AssessmentExpiryPolicy.CheckAndApplyExpiry(assessment))
        {
            assessment.Status = PreMissionAssessmentStatus.Expired;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("ASSESSMENT_EXPIRED", "Khung giờ bay hoặc thời hạn của bản đánh giá đã hết hạn, không thể đánh giá lại.");
        }

        // Step 1: Site Feasibility
        var assetIds = assessment.Assets.Select(x => x.AssetId).ToList();
        var assets = await _db.Assets
            .Where(x => assetIds.Contains(x.Id) &&
                        (x.Status == "Active" || x.Status == "Operational") &&
                        x.Tower!.TransmissionLine!.Substation!.RegionAssetId == assessment.RegionId)
            .ToListAsync(ct);

        var region = assessment.Region ?? await _db.Regions.SingleAsync(x => x.Id == assessment.RegionId, ct);
        var siteResult = SiteFeasibilityPolicy.Evaluate(region, assets, assessment.ProposedBoundary, assetIds);
        assessment.SiteFeasibilityStatus = siteResult.Status;
        assessment.Findings = siteResult.FindingsJson;

        // Step 2: Clear old candidates (Gap #4)
        if (assessment.PersonnelCandidates.Count > 0)
        {
            _db.PreMissionAssessmentPersonnel.RemoveRange(assessment.PersonnelCandidates);
            assessment.PersonnelCandidates.Clear();
        }
        if (assessment.DroneCandidates.Count > 0)
        {
            _db.PreMissionAssessmentDrones.RemoveRange(assessment.DroneCandidates);
            assessment.DroneCandidates.Clear();
        }

        // Step 3: Evaluate Personnel Candidates (Gap #3 & INF-01 Optimization)
        var isGlobal = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
        var nonOperationalRoles = new[] { UserRoles.SystemAdmin, UserRoles.Manager, "SystemAdmin", "Manager" };
        var operationalRoles = new[] { UserRoles.Inspector, UserRoles.Analyst, "Technician", UserRoles.MaintenanceTechnician, "Pilot" };

        var candidateUsers = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(x => x.IsEmailVerified && (x.Status == "Active" || x.Status == "Enabled"))
            .Where(x => !x.UserRoles.Any(r => r.Role != null && nonOperationalRoles.Contains(r.Role.RoleName)))
            .Where(x => x.UserRoles.Any(r => r.Role != null && operationalRoles.Contains(r.Role.RoleName)))
            .ToListAsync(ct);

        var candidateUserIds = candidateUsers.Select(u => u.Id).ToList();

        var userScopes = await _db.UserGeographicScopes
            .Where(s => candidateUserIds.Contains(s.UserId))
            .ToListAsync(ct);

        var userBookings = await _db.ResourceBookings
            .Where(b => b.UserId.HasValue &&
                        candidateUserIds.Contains(b.UserId.Value) &&
                        b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < assessment.PlannedEnd &&
                        b.EndAt > assessment.PlannedStart)
            .ToListAsync(ct);

        foreach (var user in candidateUsers)
        {
            var candidate = PersonnelEligibilityPolicy.EvaluatePersonnel(
                user,
                assessment.RegionId,
                assessment.PlannedStart,
                assessment.PlannedEnd,
                userScopes,
                userBookings,
                isGlobal);

            candidate.Assessment = assessment;
            candidate.AssessmentId = assessment.Id;
            candidate.User = user;
            _db.PreMissionAssessmentPersonnel.Add(candidate);
        }

        // Step 4: Evaluate Drone Candidates (Gap #5, Gap #7 & INF-01 Optimization)
        var drones = await _db.Uavs
            .Include(x => x.TechnicalInspections
                .Where(t => !t.IsDeleted && t.Status == DroneTechnicalInspectionStatus.Passed && t.ValidUntil > assessment.PlannedStart))
            .Where(x => !x.IsDeleted)
            .ToListAsync(ct);

        var droneIds = drones.Select(d => d.Id).ToList();

        var droneBookings = await _db.ResourceBookings
            .Where(b => b.DroneId.HasValue &&
                        droneIds.Contains(b.DroneId.Value) &&
                        b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < assessment.PlannedEnd &&
                        b.EndAt > assessment.PlannedStart)
            .ToListAsync(ct);

        foreach (var drone in drones)
        {
            var hasConflict = droneBookings.Any(b => b.DroneId == drone.Id);
            var isOpAvailable = drone.OperationalStatus == DroneOperationalStatus.Available && !hasConflict;

            var latestInspection = drone.TechnicalInspections
                .Where(x => x.Status == DroneTechnicalInspectionStatus.Passed && x.ValidUntil > assessment.PlannedStart)
                .OrderByDescending(x => x.CompletedAt)
                .FirstOrDefault();

            var isTechEligible = latestInspection != null &&
                                 (latestInspection.Health is TechnicalHealth.Healthy or TechnicalHealth.Warning);

            var isEligible = isOpAvailable && isTechEligible;

            string? reasonCode = null;
            if (drone.OperationalStatus != DroneOperationalStatus.Available)
                reasonCode = "DRONE_NOT_AVAILABLE";
            else if (hasConflict)
                reasonCode = "SCHEDULE_CONFLICT";
            else if (latestInspection == null)
                reasonCode = "NO_VALID_TECHNICAL_INSPECTION";
            else if (!isTechEligible)
                reasonCode = "TECHNICAL_HEALTH_DEGRADED";

            var droneCandidate = new PreMissionAssessmentDrone
            {
                Assessment = assessment,
                AssessmentId = assessment.Id,
                Drone = drone,
                DroneId = drone.Id,
                IsEligible = isEligible,
                OperationalAvailabilityStatus = isOpAvailable
                    ? ResourceAvailabilityStatus.Available
                    : ResourceAvailabilityStatus.Unavailable,
                TechnicalEligibilityStatus = isTechEligible
                    ? ResourceEligibilityStatus.Eligible
                    : ResourceEligibilityStatus.Ineligible,
                TechnicalHealth = latestInspection?.Health ?? drone.TechnicalHealth,
                TechnicalInspectionId = latestInspection?.Id,
                ReasonCode = reasonCode,
                SnapshotAt = DateTime.UtcNow
            };

            _db.PreMissionAssessmentDrones.Add(droneCandidate);
        }

        // Step 5: Overall Status Calculation
        var isSiteFeasible = siteResult.Status == ReadinessCheckStatus.Passed;
        var hasEligibleInspector = assessment.PersonnelCandidates.Any(x => x.IsEligible && (x.Role.Contains("Inspector", StringComparison.OrdinalIgnoreCase) || x.Role.Contains("Pilot", StringComparison.OrdinalIgnoreCase)));
        var hasEligibleAnalyst = assessment.PersonnelCandidates.Any(x => x.IsEligible && x.Role.Contains("Analyst", StringComparison.OrdinalIgnoreCase));
        var hasEligibleTechnician = assessment.PersonnelCandidates.Any(x => x.IsEligible && (x.Role.Contains("Tech", StringComparison.OrdinalIgnoreCase) || x.Role.Contains("Maintenance", StringComparison.OrdinalIgnoreCase)));
        var hasEligiblePersonnel = hasEligibleInspector && hasEligibleAnalyst && hasEligibleTechnician;
        var hasEligibleDrone = assessment.DroneCandidates.Any(x => x.IsEligible);

        var isReady = isSiteFeasible && hasEligiblePersonnel && hasEligibleDrone;
        assessment.Status = isReady ? PreMissionAssessmentStatus.Ready : PreMissionAssessmentStatus.NotReady;
        var expiryCandidate = DateTime.UtcNow.AddHours(4);
        assessment.ValidUntil = expiryCandidate < assessment.PlannedEnd ? expiryCandidate : assessment.PlannedEnd;
        assessment.Version++;
        assessment.EvaluationPolicyVersion = "v2.0";

        var bestDroneHealth = assessment.DroneCandidates
            .Where(x => x.IsEligible)
            .Select(x => x.TechnicalHealth)
            .FirstOrDefault();
        assessment.OverallTechnicalHealth = bestDroneHealth != TechnicalHealth.Unknown ? bestDroneHealth : (hasEligibleDrone ? TechnicalHealth.Healthy : TechnicalHealth.Unknown);

        Audit(assessment.Id, "ASSESSMENT_EVALUATED");
        await SaveWithConcurrency(ct);
        return assessment;
    }

    public async Task<Mission> CreateMissionFromAssessmentAsync(CreateMissionFromAssessmentRequest request, CancellationToken ct)
    {
        await RequireManager(ct);

        // Gap #19: Idempotency
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingMission = await _db.Missions
                .Include(x => x.Assignments)
                .Include(x => x.MissionTargets)
                .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
            if (existingMission != null)
                return existingMission;
        }

        var assessment = await _db.PreMissionAssessments
            .Include(x => x.Assets)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .SingleOrDefaultAsync(x => x.Id == request.AssessmentId, ct)
            ?? throw new NotFoundException("PreMissionAssessment", request.AssessmentId);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (assessment.Status == PreMissionAssessmentStatus.Completed ||
            assessment.ConsumedByMissionId.HasValue ||
            await _db.Missions.AnyAsync(x => x.PreMissionAssessmentId == request.AssessmentId, ct))
        {
            throw new BusinessRuleException("ASSESSMENT_ALREADY_COMPLETED", "Đánh giá tiền nhiệm vụ đã hoàn thành tạo nhiệm vụ bay.");
        }

        // Gap #9: Expiry on use
        if (AssessmentExpiryPolicy.CheckAndApplyExpiry(assessment))
        {
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("ASSESSMENT_EXPIRED");
        }

        if (assessment.Status != PreMissionAssessmentStatus.Ready || assessment.ValidUntil <= DateTime.UtcNow)
            throw new BusinessRuleException("ASSESSMENT_EXPIRED_OR_NOT_READY");

        if (request.Personnel == null || request.Personnel.Count == 0)
            throw new BusinessRuleException("PERSONNEL_ASSIGNMENT_REQUIRED");

        if (request.DroneIds == null || request.DroneIds.Count == 0)
            throw new BusinessRuleException("DRONE_ASSIGNMENT_REQUIRED");

        // Gap #10: Validate candidates
        foreach (var p in request.Personnel)
        {
            var candidate = assessment.PersonnelCandidates.FirstOrDefault(x => x.UserId == p.UserId && x.IsEligible);
            if (candidate == null)
                throw new BusinessRuleException("RESOURCE_NOT_IN_ASSESSMENT", $"Personnel {p.UserId} is not an eligible candidate in assessment.");
        }

        foreach (var droneId in request.DroneIds)
        {
            var candidate = assessment.DroneCandidates.FirstOrDefault(x => x.DroneId == droneId && x.IsEligible);
            if (candidate == null)
                throw new BusinessRuleException("RESOURCE_NOT_IN_ASSESSMENT", $"Drone {droneId} is not an eligible candidate in assessment.");
        }

        if (request.Priority == MissionPriority.Emergency)
        {
            if (string.IsNullOrWhiteSpace(request.EmergencyReason) || request.EmergencyReason.Trim().Length < 10)
            {
                throw new BusinessRuleException("EMERGENCY_REASON_REQUIRED", "EmergencyReason is required (min 10 characters) when Priority is EMERGENCY.");
            }
        }

        // Gap #10: Check real-time resource bookings conflict (BR-04, BR-07)
        var assignedUserIds = request.Personnel.Select(p => p.UserId).ToList();
        var conflictingUserBookings = await _db.ResourceBookings
            .Include(b => b.Mission)
            .Where(b => b.UserId.HasValue && assignedUserIds.Contains(b.UserId.Value) &&
                        b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < assessment.PlannedEnd &&
                        b.EndAt > assessment.PlannedStart)
            .ToListAsync(ct);
        if (conflictingUserBookings.Count > 0)
        {
            var conflictDetails = string.Join("; ", conflictingUserBookings.Select(b =>
                $"Pilot {b.UserId} is scheduled for mission {b.Mission?.MissionCode ?? b.MissionId.ToString()} ({b.StartAt:yyyy-MM-dd HH:mm} - {b.EndAt:yyyy-MM-dd HH:mm})"));
            throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT", $"One or more assigned personnel have a conflicting schedule. {conflictDetails}");
        }

        var conflictingDroneBookings = await _db.ResourceBookings
            .Include(b => b.Mission)
            .Where(b => b.DroneId.HasValue && request.DroneIds.Contains(b.DroneId.Value) &&
                        b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < assessment.PlannedEnd &&
                        b.EndAt > assessment.PlannedStart)
            .ToListAsync(ct);
        if (conflictingDroneBookings.Count > 0)
        {
            var conflictDetails = string.Join("; ", conflictingDroneBookings.Select(b =>
                $"Drone {b.DroneId} is scheduled for mission {b.Mission?.MissionCode ?? b.MissionId.ToString()} ({b.StartAt:yyyy-MM-dd HH:mm} - {b.EndAt:yyyy-MM-dd HH:mm})"));
            throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT", $"One or more assigned drones have a conflicting schedule. {conflictDetails}");
        }

        var primaryDroneId = request.DroneIds.First();
        var primaryDrone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == primaryDroneId, ct)
            ?? throw new NotFoundException("Drone", primaryDroneId);

        var primaryInspectorId = request.Personnel.First().UserId;

        // Gap #11: Mission starts at PendingAcceptance
        var mission = new Mission
        {
            MissionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{RandomNumberGenerator.GetInt32(100, 1000)}",
            Title = request.Title,
            Description = request.Description ?? string.Empty,
            ManagerId = _current.UserId,
            InspectorId = primaryInspectorId,
            AssignedToUserId = primaryInspectorId,
            UavId = primaryDrone.Id,
            DroneCode = primaryDrone.UavCode,
            RegionId = assessment.RegionId,
            PlannedStart = assessment.PlannedStart,
            PlannedEnd = assessment.PlannedEnd,
            ScheduledStartAt = assessment.PlannedStart,
            Priority = request.Priority,
            Objective = request.Objective,
            PriorityDefectsJson = JsonSerializer.Serialize(request.PriorityDefects ?? (IEnumerable<string>)Array.Empty<string>()),
            EmergencyReason = request.EmergencyReason,
            IsImmediate = request.IsImmediate,
            Status = MissionStatus.PendingAcceptance,
            PreMissionAssessmentId = assessment.Id,
            Boundary = assessment.ProposedBoundary,
            IdempotencyKey = request.IdempotencyKey
        };

        // Targets
        mission.MissionTargets = assessment.Assets
            .OrderBy(x => x.Sequence)
            .Select(x => new MissionTarget
            {
                MissionId = mission.Id,
                AssetId = x.AssetId,
                Sequence = x.Sequence
            })
            .ToList();

        // Assignments
        foreach (var p in request.Personnel)
        {
            mission.Assignments.Add(new MissionAssignment
            {
                MissionId = mission.Id,
                UserId = p.UserId,
                AssignmentRole = p.Role,
                Status = MissionAssignmentStatus.Active,
                ResponseStatus = MissionAssignmentResponse.Pending,
                IsRequired = p.IsRequired,
                AssignedByUserId = _current.UserId,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Resource Bookings
        foreach (var p in request.Personnel)
        {
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = mission.Id,
                UserId = p.UserId,
                StartAt = assessment.PlannedStart,
                EndAt = assessment.PlannedEnd,
                Status = ResourceBookingStatus.Active
            });
        }
        foreach (var droneId in request.DroneIds)
        {
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = mission.Id,
                DroneId = droneId,
                StartAt = assessment.PlannedStart,
                EndAt = assessment.PlannedEnd,
                Status = ResourceBookingStatus.Active
            });
        }

        // Consume assessment
        assessment.Status = PreMissionAssessmentStatus.Completed;
        assessment.ConsumedByMissionId = mission.Id;
        assessment.Version++;

        _db.Missions.Add(mission);

        // Gap #12: Outbox Event
        _db.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = "MissionCreatedFromAssessment",
            Payload = JsonSerializer.Serialize(new MissionCreatedFromAssessmentEvent
            {
                MissionId = mission.Id,
                AssessmentId = assessment.Id,
                ManagerId = _current.UserId,
                Title = mission.Title,
                PlannedStart = mission.PlannedStart ?? assessment.PlannedStart,
                PlannedEnd = mission.PlannedEnd ?? assessment.PlannedEnd,
                PersonnelCount = request.Personnel.Count,
                DroneCount = request.DroneIds.Count
            }),
            OccurredAt = DateTime.UtcNow
        });

        // Audit & Notification
        Audit(mission.Id, "MISSION_CREATED_FROM_ASSESSMENT", "{}", JsonSerializer.Serialize(new
        {
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = request.PriorityDefects,
            EmergencyReason = mission.EmergencyReason,
            IsImmediate = mission.IsImmediate
        }), "Missions");
        foreach (var p in request.Personnel)
        {
            Notify(p.UserId, mission, "MISSION_DISPATCH", p.Role);
        }

        await SaveWithConcurrency(ct);
        return mission;
    }

    public async Task<Mission> CreateMissionAsync(
        Guid assessmentId,
        string title,
        Guid inspectorId,
        Guid droneId,
        CancellationToken ct)
    {
        var request = new CreateMissionFromAssessmentRequest(
            assessmentId,
            title,
            null,
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { droneId }
        );
        return await CreateMissionFromAssessmentAsync(request, ct);
    }

    public async Task<PreMissionAssessment> MarkCompletedAsync(Guid assessmentId, Guid? missionId, CancellationToken ct)
    {
        await RequireManager(ct);

        var assessment = await _db.PreMissionAssessments
            .Include(x => x.Assets)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == assessmentId, ct)
            ?? throw new NotFoundException("PreMissionAssessment", assessmentId);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (assessment.Status == PreMissionAssessmentStatus.Completed || assessment.ConsumedByMissionId.HasValue)
            throw new BusinessRuleException("ASSESSMENT_ALREADY_COMPLETED");

        if (assessment.Status != PreMissionAssessmentStatus.Ready)
            throw new BusinessRuleException("INVALID_ASSESSMENT_STATUS", "Chỉ bản đánh giá ở trạng thái READY mới có thể đánh dấu hoàn thành.");

        if (!missionId.HasValue || missionId.Value == Guid.Empty)
            throw new BusinessRuleException("MISSION_REQUIRED", "Cần cung cấp mã nhiệm vụ hợp lệ để hoàn tất bản đánh giá.");

        var mission = await _db.Missions.SingleOrDefaultAsync(m => m.Id == missionId.Value, ct)
            ?? throw new NotFoundException("Mission", missionId.Value);

        if (mission.PreMissionAssessmentId != assessmentId)
        {
            throw new BusinessRuleException("INVALID_MISSION_REFERENCE", "Nhiệm vụ được tham chiếu không thuộc về bản đánh giá này.");
        }

        assessment.ConsumedByMissionId = mission.Id;
        assessment.Status = PreMissionAssessmentStatus.Completed;
        assessment.Version++;

        Audit(assessment.Id, "ASSESSMENT_MARKED_COMPLETED");
        await SaveWithConcurrency(ct);
        return assessment;
    }

    private async Task RequireManager(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isManager = _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);

        if (!isManager)
            throw new ForbiddenException("ASSESSMENT_PERMISSION_REQUIRED");

        if (!await _db.Users.AnyAsync(x => x.Id == _current.UserId && (x.Status == "Active" || x.Status == "Enabled"), ct))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");
    }

    private async Task EnsureManagerHasAssessmentAccessAsync(PreMissionAssessment assessment, CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        if (assessment.ManagerId == _current.UserId)
            return;

        var isManager = _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase);
        if (isManager && await _db.UserGeographicScopes.AnyAsync(
            s => s.UserId == _current.UserId && (s.RegionId == assessment.RegionId || s.RegionId == null), ct))
        {
            return;
        }

        throw new ForbiddenException("ASSESSMENT_ACCESS_DENIED");
    }

    private void Audit(Guid id, string action, string oldValues = "{}", string newValues = "{}", string tableName = "PreMissionAssessments") =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = tableName,
            RecordId = id,
            ActionType = action,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = _current.IpAddress ?? "",
            UserAgent = _current.UserAgent ?? ""
        });

    private void Notify(Guid userId, Mission m, string type, string? role = null)
    {
        var roleText = !string.IsNullOrWhiteSpace(role) ? $"vai trò {role}" : "nhiệm vụ";
        var deadlineText = m.ConfirmationDeadline.HasValue 
            ? $" Hạn chót xác nhận: {m.ConfirmationDeadline.Value:dd/MM/yyyy HH:mm}." 
            : string.Empty;
        var instructionsText = !string.IsNullOrWhiteSpace(m.ManagerInstructions)
            ? $" Lời dặn: \"{m.ManagerInstructions}\""
            : string.Empty;

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type == "MISSION_DISPATCHED" ? "MISSION_DISPATCH" : type,
            ReferenceType = "MISSION",
            ReferenceId = m.Id,
            Title = $"[MF02 ĐIỀU PHỐI] Yêu cầu xác nhận nhiệm vụ: {m.MissionCode}",
            Body = $"Bạn được phân công tham gia {roleText} trong nhiệm vụ \"{m.Title}\".{deadlineText}{instructionsText}",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
    }

    private async Task SaveWithConcurrency(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityNames = string.Join(", ", ex.Entries.Select(e => $"{e.Metadata.ClrType.Name} ({e.State})"));
            throw new BusinessRuleException("ASSESSMENT_CONCURRENCY_CONFLICT", $"{entityNames}: {ex.Message}");
        }
    }
}
