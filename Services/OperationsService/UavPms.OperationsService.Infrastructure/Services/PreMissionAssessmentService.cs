using UavPms.OperationsService.Infrastructure.Repositories;
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
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class PreMissionAssessmentService : IPreMissionAssessmentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    private readonly IMissionRealtimeNotifier? _notifier;

    public PreMissionAssessmentService(
        ApplicationDbContext db, 
        ICurrentUserServices current,
        IMissionRealtimeNotifier? notifier = null)
    {
        _db = db;
        _current = current;
        _notifier = notifier;
    }

    public async Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> towerIds,
        CancellationToken ct)
    {
        return await CreateAsync(regionId, plannedStart, plannedEnd, towerIds, null, null, ct);
    }

    public async Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> towerIds,
        string? boundaryWkt,
        string? idempotencyKey,
        CancellationToken ct)
    {
        await RequireManager(ct);

        // BR-14: a missing tower list is an empty scope (400), not a NullReferenceException (500).
        if (towerIds == null || towerIds.Count == 0 || towerIds.Any(id => id == Guid.Empty) ||
            towerIds.Count != towerIds.Distinct().Count())
            throw new BusinessRuleException("INVALID_PROPOSED_SCOPE", "Cần chọn ít nhất một cột điện và không được trùng lặp.");

        // BR-08: times are stored in UTC. A value without an offset is taken as UTC, one with an offset is converted.
        plannedStart = ToUtc(plannedStart);
        plannedEnd = ToUtc(plannedEnd);
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

        if (idempotencyKey != null)
        {
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing != null)
                return await ReplayIdempotentCreateAsync(existing, regionId, plannedStart, plannedEnd, towerIds, boundaryWkt, ct);
        }

        if (plannedEnd <= plannedStart || plannedEnd <= DateTime.UtcNow)
            throw new BusinessRuleException("INVALID_PLANNED_TIME", "Thời điểm kết thúc phải sau thời điểm bắt đầu và ở tương lai.");

        // A soft-deleted (inactive) region is hidden by the query filter, so it is reported as 404 Region not found.
        var region = await _db.Regions.SingleOrDefaultAsync(x => x.Id == regionId, ct)
            ?? throw new NotFoundException("Region", regionId);

        if (!_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) &&
            !await _db.UserGeographicScopes.AnyAsync(x => x.UserId == _current.UserId && x.RegionId == regionId, ct))
            throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED");

        var canonicalAssets = await _db.Towers
            .Where(x => towerIds.Contains(x.Id) &&
                        (x.RegionId == regionId || x.Region!.ParentId == regionId))
            .ToListAsync(ct);

        if (canonicalAssets.Count != towerIds.Count)
            throw new ForbiddenException("TOWER_OUTSIDE_MANAGEMENT_SCOPE");

        Geometry? boundary = null;
        if (!string.IsNullOrWhiteSpace(boundaryWkt))
        {
            boundary = SiteFeasibilityPolicy.ParseBoundary(boundaryWkt);
        }

        var siteResult = SiteFeasibilityPolicy.Evaluate(region, canonicalAssets, boundary, towerIds);
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

        assessment.Towers = towerIds
            .Select((id, i) => new PreMissionAssessmentTower
            {
                Assessment = assessment,
                TowerId = id,
                Sequence = i + 1
            })
            .ToList();

        _db.PreMissionAssessments.Add(assessment);
        Audit(assessment.Id, "ASSESSMENT_CREATED");
        try
        {
            await SaveWithConcurrency(ct);
        }
        catch (DbUpdateException ex) when (idempotencyKey != null && IsIdempotencyKeyViolation(ex))
        {
            // A concurrent request with the same key won the insert. Only this unique violation is an idempotency
            // conflict; any other database error propagates.
            _db.ChangeTracker.Clear();
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing == null)
                throw new BusinessRuleException("IDEMPOTENCY_CONFLICT", "An assessment with the same idempotency key is currently being processed.");

            return await ReplayIdempotentCreateAsync(existing, regionId, plannedStart, plannedEnd, towerIds, boundaryWkt, ct);
        }

        try
        {
            return await EvaluateAsync(assessment.Id, ct);
        }
        catch (Exception ex) when (idempotencyKey != null && IsConcurrentEvaluation(ex))
        {
            // A retry with the same key evaluated this assessment at the same time; return the stored result.
            _db.ChangeTracker.Clear();
            var reloaded = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (reloaded == null)
                throw;
            return reloaded;
        }
    }

    /// <summary>Only a unique violation of the idempotency index (IX_PreMissionAssessments_IdempotencyKey in the
    /// migrations; matched by name part because the production schema was partly hand-patched).</summary>
    private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } pg &&
        pg.ConstraintName != null &&
        pg.ConstraintName.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase) &&
        (pg.TableName == null || pg.TableName == "PreMissionAssessments");

    private Task<PreMissionAssessment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        _db.PreMissionAssessments
            .Include(a => a.Mission)
            .Include(x => x.Towers).ThenInclude(a => a.Tower)
            .Include(x => x.PersonnelCandidates).ThenInclude(p => p.User)
            .Include(x => x.DroneCandidates).ThenInclude(d => d.Drone)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    /// <summary>
    /// UC-MF01-01 A1: a repeated submission returns the existing assessment. The key belongs to the manager who
    /// created it (the unique index is global, so another manager's key is reported as reused), the payload must be
    /// the same, the caller must still have write access, and an assessment left in Evaluating by a failed
    /// evaluation is evaluated again.
    /// </summary>
    private async Task<PreMissionAssessment> ReplayIdempotentCreateAsync(
        PreMissionAssessment existing,
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> towerIds,
        string? boundaryWkt,
        CancellationToken ct)
    {
        if (existing.ManagerId != _current.UserId)
            throw new BusinessRuleException("IDEMPOTENCY_KEY_REUSED", "Khóa idempotency đã được dùng cho một yêu cầu khác.");

        var boundary = string.IsNullOrWhiteSpace(boundaryWkt) ? null : SiteFeasibilityPolicy.ParseBoundary(boundaryWkt);
        var sameBoundary = (boundary == null && existing.ProposedBoundary == null) ||
                           (boundary != null && existing.ProposedBoundary != null && boundary.EqualsTopologically(existing.ProposedBoundary));
        var samePayload = existing.RegionId == regionId &&
                          SameInstant(existing.PlannedStart, plannedStart) &&
                          SameInstant(existing.PlannedEnd, plannedEnd) &&
                          existing.Towers.Select(t => t.TowerId).ToHashSet().SetEquals(towerIds) &&
                          existing.Towers.Count == towerIds.Count &&
                          sameBoundary;
        if (!samePayload)
            throw new BusinessRuleException("IDEMPOTENCY_KEY_REUSED", "Khóa idempotency đã được dùng với vùng, khung giờ hoặc danh sách cột khác.");

        await EnsureManagerHasAssessmentAccessAsync(existing, ct);

        if (existing.Status is PreMissionAssessmentStatus.Draft or PreMissionAssessmentStatus.Evaluating)
        {
            if (await ApplyExpiryAsync(existing, ct))
                return existing;
            try
            {
                return await EvaluateAsync(existing.Id, ct);
            }
            catch (Exception ex) when (IsConcurrentEvaluation(ex))
            {
                // The original request is evaluating the same assessment right now; return its current state.
                _db.ChangeTracker.Clear();
                return await FindByIdempotencyKeyAsync(existing.IdempotencyKey!, ct) ?? existing;
            }
        }

        await ApplyExpiryAsync(existing, ct);
        return existing;
    }

    /// <summary>Two evaluations of one assessment at once: the version check or the candidate unique indexes stop the second.</summary>
    private static bool IsConcurrentEvaluation(Exception ex) =>
        ex is BusinessRuleException { Code: "ASSESSMENT_CONCURRENCY_CONFLICT" } ||
        ex is DbUpdateConcurrencyException ||
        (ex is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } pg } &&
         pg.ConstraintName != null &&
         (pg.ConstraintName.StartsWith("IX_PreMissionAssessmentPersonnel_", StringComparison.Ordinal) ||
          pg.ConstraintName.StartsWith("IX_PreMissionAssessmentDrones_", StringComparison.Ordinal)));

    /// <summary>PostgreSQL keeps microseconds; .NET keeps 100 ns ticks.</summary>
    private static bool SameInstant(DateTime a, DateTime b) =>
        Math.Abs((ToUtc(a) - ToUtc(b)).Ticks) < TimeSpan.TicksPerMillisecond;

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    /// <summary>BR-17 / NS-01: sets an outdated assessment to Expired and audits it. Returns true when it expired now.</summary>
    private async Task<bool> ApplyExpiryAsync(PreMissionAssessment assessment, CancellationToken ct)
    {
        if (!AssessmentExpiryPolicy.CheckAndApplyExpiry(assessment))
            return false;

        Audit(assessment.Id, "ASSESSMENT_EXPIRED");
        await SaveWithConcurrency(ct);
        return true;
    }

    public async Task<PreMissionAssessment> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireAssessmentReader(ct);

        var assessment = await _db.PreMissionAssessments
            .Include(a => a.Mission)
            .Include(x => x.Towers).ThenInclude(a => a.Tower)
            .Include(x => x.PersonnelCandidates).ThenInclude(p => p.User)
            .Include(x => x.DroneCandidates).ThenInclude(d => d.Drone)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PreMissionAssessment", id);

        await EnsureAssessmentReadAccessAsync(assessment, ct);

        await ApplyExpiryAsync(assessment, ct);

        return assessment;
    }

    public async Task<IReadOnlyList<PreMissionAssessment>> ListAsync(CancellationToken ct)
    {
        return await ListAsync(null, ct);
    }

    public async Task<IReadOnlyList<PreMissionAssessment>> ListAsync(string? status, CancellationToken ct)
    {
        await RequireAssessmentReader(ct);

        var isGlobal = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
        var isInspector = IsFieldReader();
        // UserGeographicScope.RegionId is non-nullable: there is no "global scope" row; SystemAdmin sees everything.
        var userScopes = isGlobal
            ? new List<Guid>()
            : await _db.UserGeographicScopes
                .Where(x => x.UserId == _current.UserId)
                .Select(x => x.RegionId)
                .ToListAsync(ct);

        IQueryable<PreMissionAssessment> visible = _db.PreMissionAssessments
            .Where(x => isGlobal ||
                        isInspector ||
                        x.ManagerId == _current.UserId ||
                        userScopes.Contains(x.RegionId));

        // BR-17 / NS-01: expire (and audit) before filtering by status, so ?status=NOT_READY never returns a row
        // that is expired by the time it is read.
        var now = DateTime.UtcNow;
        var due = await visible
            .Where(x => (x.Status == PreMissionAssessmentStatus.Draft ||
                         x.Status == PreMissionAssessmentStatus.Evaluating ||
                         x.Status == PreMissionAssessmentStatus.Ready ||
                         x.Status == PreMissionAssessmentStatus.NotReady) &&
                        ((x.ValidUntil != null && x.ValidUntil <= now) || x.PlannedEnd <= now))
            .ToListAsync(ct);
        if (due.Count > 0)
        {
            foreach (var a in due)
            {
                if (AssessmentExpiryPolicy.CheckAndApplyExpiry(a))
                    Audit(a.Id, "ASSESSMENT_EXPIRED");
            }
            await SaveWithConcurrency(ct);
        }

        var query = visible
            .Include(a => a.Mission)
            .Include(x => x.Towers)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .AsQueryable();

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

        // Rows that crossed their validity between the sweep above and this read.
        var expiredAny = false;
        foreach (var a in list)
        {
            if (AssessmentExpiryPolicy.CheckAndApplyExpiry(a))
            {
                Audit(a.Id, "ASSESSMENT_EXPIRED");
                expiredAny = true;
            }
        }

        if (expiredAny)
            await SaveWithConcurrency(ct);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = NormalizeAssessmentStatusFilter(status);
            list = list.Where(x => x.Status == parsed).ToList();
        }

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
            .Include(a => a.Mission)
            .Include(x => x.Towers)
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

        // BR-17: expired assessments cannot be evaluated; the expiry is applied (and audited) first.
        if (assessment.Status == PreMissionAssessmentStatus.Expired || await ApplyExpiryAsync(assessment, ct))
            throw new BusinessRuleException("ASSESSMENT_EXPIRED", "Khung giờ bay hoặc thời hạn của bản đánh giá đã hết hạn, không thể đánh giá lại.");

        // Step 1: Site Feasibility
        var towerIds = assessment.Towers.OrderBy(x => x.Sequence).Select(x => x.TowerId).ToList();
        var towers = await _db.Towers
            .Where(x => towerIds.Contains(x.Id) &&
                        (x.RegionId == assessment.RegionId || x.Region!.ParentId == assessment.RegionId))
            .ToListAsync(ct);

        // A region soft-deleted after the assessment was created is hidden by the query filter.
        var region = assessment.Region ?? await _db.Regions.SingleOrDefaultAsync(x => x.Id == assessment.RegionId, ct)
            ?? throw new BusinessRuleException("REGION_INACTIVE", "Khu vực của bản đánh giá không còn hoạt động.");
        var siteResult = SiteFeasibilityPolicy.Evaluate(region, towers, assessment.ProposedBoundary, towerIds);
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
        // BR-18: the candidates are the field users (no SystemAdmin/Manager role). Active users are all listed; an
        // inactive or unverified one is listed only when they have a scope row for the region, so the manager sees
        // USER_INACTIVE for the region's own staff without the list growing with every disabled account.
        // A candidate's scope comes from the candidate's own scope rows, never from the caller's role.
        var nonOperationalRoles = PersonnelEligibilityPolicy.NonFieldRoleNames.ToArray();
        var operationalRoles = PersonnelEligibilityPolicy.FieldRoleNames.ToArray();
        var regionStaffIds = _db.UserGeographicScopes
            .Where(s => s.RegionId == assessment.RegionId)
            .Select(s => s.UserId);

        var candidateUsers = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(x => !x.UserRoles.Any(r => r.Role != null && nonOperationalRoles.Contains(r.Role.RoleName)))
            .Where(x => x.UserRoles.Any(r => r.Role != null && operationalRoles.Contains(r.Role.RoleName)))
            .Where(x => (x.IsEmailVerified && (x.Status == "Active" || x.Status == "Enabled")) ||
                        regionStaffIds.Contains(x.Id))
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
                userBookings);

            candidate.Assessment = assessment;
            candidate.AssessmentId = assessment.Id;
            candidate.User = user;
            _db.PreMissionAssessmentPersonnel.Add(candidate);
        }

        // Step 4: UAV candidates (BR-20 – BR-22). Same evaluator as GET /drones/eligible: newest completed inspection
        // of any result, valid at the planned start; scope suitability from the real tower corridor.
        var (corridorKm, towerCount) = await DroneReadinessEvaluator.EstimateScopeAsync(_db, assessment.Towers, ct);
        var droneResults = await DroneReadinessEvaluator.EvaluateAsync(
            _db, assessment.PlannedStart, assessment.PlannedEnd, corridorKm, towerCount, ct);

        foreach (var (drone, result) in droneResults)
        {
            var droneCandidate = new PreMissionAssessmentDrone
            {
                Assessment = assessment,
                AssessmentId = assessment.Id,
                Drone = drone,
                DroneId = drone.Id,
                OperationalAvailabilityStatus = result.IsOperationallyAvailable
                    ? ResourceAvailabilityStatus.Available
                    : ResourceAvailabilityStatus.Unavailable,
                TechnicalEligibilityStatus = result.TechnicalEligibility,
                Suitability = result.Scope.Suitability,
                EstimatedFlightsNeeded = result.Scope.EstimatedFlightsNeeded,
                CoverageDistanceKm = result.Scope.MaxCoverageDistanceKm,
                SuitabilityReason = result.Scope.Reason,
                TechnicalHealth = result.TechnicalHealth,
                TechnicalInspectionId = result.LatestInspection?.Id,
                ReasonCode = result.ReasonCode,
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

        var eligibleHealths = assessment.DroneCandidates.Where(x => x.IsEligible).Select(x => x.TechnicalHealth).ToList();
        assessment.OverallTechnicalHealth = eligibleHealths.Contains(TechnicalHealth.Healthy)
            ? TechnicalHealth.Healthy
            : eligibleHealths.Contains(TechnicalHealth.Warning)
                ? TechnicalHealth.Warning
                : TechnicalHealth.Unknown;

        Audit(assessment.Id, "ASSESSMENT_EVALUATED");
        await SaveWithConcurrency(ct);

        if (_notifier != null)
        {
            await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
            {
                AssessmentId = assessment.Id.ToString(),
                Type = "AssessmentStatusChanged",
                Status = assessment.Status.ToString().ToUpperInvariant(),
                TechnicalHealth = assessment.OverallTechnicalHealth.ToString(),
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Manager",
                ActorRole = "MANAGER",
                Timestamp = DateTime.UtcNow,
                Message = $"Thẩm định tiền nhiệm vụ {assessment.Id} đã được đánh giá lại: {assessment.Status}"
            }, ct);
        }

        return assessment;
    }

    public async Task<Mission> CreateMissionFromAssessmentAsync(CreateMissionFromAssessmentRequest request, CancellationToken ct)
    {
        await RequireManager(ct);

        // Gap #19 / UC-MF02-01 A1: a repeated key returns the mission it created, but only for the same manager,
        // assessment, title and UAV; any other reuse of the key is a conflict.
        async Task<Mission?> FindIdempotentReplayAsync()
        {
            var existing = await _db.Missions
                .Include(x => x.Assignments)
                .Include(x => x.MissionTargets)
                .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing == null) return null;
            var sameRequest = existing.ManagerId == _current.UserId
                && existing.PreMissionAssessmentId == request.AssessmentId
                && string.Equals(existing.Title, request.Title, StringComparison.Ordinal)
                && existing.UavId == request.DroneIds?.FirstOrDefault();
            if (!sameRequest)
                throw new BusinessRuleException("IDEMPOTENCY_KEY_REUSED", "This idempotency key was already used for another mission request.");
            return existing;
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await FindIdempotentReplayAsync();
            if (replay != null)
                return replay;
        }

        try
        {
            return await CreateMissionCoreAsync();
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && ex is BusinessRuleException or DbUpdateException)
        {
            // An identical request may have committed meanwhile (the assessment is then used, its people booked, or a
            // constraint refused this insert): the retry gets that mission instead of an error.
            _db.ChangeTracker.Clear();
            var replay = await FindIdempotentReplayAsync();
            if (replay != null)
                return replay;
            throw;
        }

        async Task<Mission> CreateMissionCoreAsync()
        {
            var assessment = await _db.PreMissionAssessments
                .Include(a => a.Mission)
                .Include(x => x.Towers)
                .Include(x => x.PersonnelCandidates)
                .Include(x => x.DroneCandidates)
                .SingleOrDefaultAsync(x => x.Id == request.AssessmentId, ct)
                ?? throw new NotFoundException("PreMissionAssessment", request.AssessmentId);

            await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

            if (assessment.Status == PreMissionAssessmentStatus.Completed ||
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

            // BR-26/BR-28: each person once, exactly one UAV (a mission flies one UAV and books only it), and at least
            // one required member so the mission can ever be accepted (BR-30).
            if (request.Personnel.Any(p => p.UserId == Guid.Empty))
                throw new BusinessRuleException("PERSONNEL_ASSIGNMENT_REQUIRED", "Every person needs a UserId.");
            if (request.Personnel.Select(p => p.UserId).Distinct().Count() != request.Personnel.Count)
                throw new BusinessRuleException("DUPLICATE_PERSONNEL", "A person is listed more than once.");
            if (request.DroneIds.Distinct().Count() != request.DroneIds.Count)
                throw new BusinessRuleException("DUPLICATE_DRONE", "A UAV is listed more than once.");
            if (request.DroneIds.Count > 1)
                throw new BusinessRuleException("MULTIPLE_DRONES_NOT_SUPPORTED", "A mission flies exactly one UAV; send one DroneId.");
            if (!request.Personnel.Any(p => p.IsRequired))
                throw new BusinessRuleException("REQUIRED_MEMBER_REQUIRED", "At least one person must be required.");

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

            // Gap #10 / BR-18 – BR-20: the candidate snapshot can be hours old, so every person and the UAV are checked
            // again now (account, role, scope, health, availability and BR-19 bookings, read without the soft-delete join).
            var callerIsGlobal = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
            foreach (var p in request.Personnel)
            {
                await MissionResourceGuard.EnsurePersonEligibleAsync(
                    _db, p.UserId, assessment.RegionId, assessment.PlannedStart, assessment.PlannedEnd, null, callerIsGlobal, ct);
            }

            var primaryDroneId = request.DroneIds[0];
            var primaryDrone = await MissionResourceGuard.EnsureDroneEligibleAsync(
                _db, primaryDroneId, assessment.PlannedStart, assessment.PlannedEnd, null, assessment.Id, ct);

            // BR-31: the confirmation deadline drives the overdue notification; it lies in the future and not after the start.
            var now = DateTime.UtcNow;
            DateTime confirmationDeadline;
            if (request.ConfirmationDeadline.HasValue)
            {
                var requested = request.ConfirmationDeadline.Value;
                confirmationDeadline = requested.Kind switch
                {
                    DateTimeKind.Utc => requested,
                    DateTimeKind.Local => requested.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(requested, DateTimeKind.Utc)
                };
                if (confirmationDeadline <= now)
                    throw new BusinessRuleException("INVALID_CONFIRMATION_DEADLINE", "The confirmation deadline must be in the future.");
                if (confirmationDeadline > assessment.PlannedStart)
                    throw new BusinessRuleException("INVALID_CONFIRMATION_DEADLINE", "The confirmation deadline must not be after the planned start.");
            }
            else
            {
                var hours = await MissionResourceGuard.GetIntSettingAsync(
                    _db, SettingKeys.MissionConfirmationDeadlineHours, MissionResourceGuard.DefaultConfirmationDeadlineHours, ct);
                var byDefault = now.AddHours(hours);
                var cap = assessment.PlannedStart > now ? assessment.PlannedStart : assessment.PlannedEnd;
                confirmationDeadline = byDefault < cap ? byDefault : cap;
            }

            var managerInstructions = string.IsNullOrWhiteSpace(request.ManagerInstructions) ? null : request.ManagerInstructions.Trim();
            if (managerInstructions?.Length > 2000)
                throw new BusinessRuleException("MANAGER_INSTRUCTIONS_TOO_LONG", "Manager instructions are limited to 2,000 characters.");

            var primaryInspectorId = request.Personnel
                .Where(p => string.Equals(p.Role, "INSPECTOR", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Role, "PILOT", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.UserId)
                .FirstOrDefault(request.Personnel[0].UserId);

            // Gap #11: Mission starts at PendingAcceptance
            var mission = new Mission
            {
                MissionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{RandomNumberGenerator.GetInt32(100, 1000)}",
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                ManagerId = _current.UserId,
                AssignedToUserId = primaryInspectorId,
                UavId = primaryDrone.Id,
                DroneCode = primaryDrone.UavCode,
                RegionId = assessment.RegionId,
                PlannedStart = assessment.PlannedStart,
                PlannedEnd = assessment.PlannedEnd,
                Priority = request.Priority,
                Objective = request.Objective,
                TriggerReason = request.EmergencyReason,
                IsImmediate = request.IsImmediate,
                Status = MissionStatus.PendingAcceptance,
                PreMissionAssessmentId = assessment.Id,
                Boundary = assessment.ProposedBoundary,
                IdempotencyKey = request.IdempotencyKey,
                ConfirmationDeadline = confirmationDeadline,
                ManagerInstructions = managerInstructions
            };

            // Targets
            mission.MissionTargets = assessment.Towers
                .OrderBy(x => x.Sequence)
                .Select(x => new MissionTarget
                {
                    MissionId = mission.Id,
                    TowerId = x.TowerId,
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
                    AssignedById = _current.UserId,
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
            // Only the mission UAV is booked (BR-28): one Active booking per person and UAV.
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = mission.Id,
                DroneId = primaryDrone.Id,
                StartAt = assessment.PlannedStart,
                EndAt = assessment.PlannedEnd,
                Status = ResourceBookingStatus.Active
            });

            // Complete assessment and link mission
            assessment.Status = PreMissionAssessmentStatus.Completed;
            assessment.Version++;

            await MissionRepository.ApplyPriorityDefectsAsync(_db, mission, request.PriorityDefects, ct);
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
                EmergencyReason = mission.TriggerReason,
                IsImmediate = mission.IsImmediate
            }), "Missions");
            foreach (var p in request.Personnel)
            {
                Notify(p.UserId, mission, "MISSION_DISPATCH", p.Role);
            }

            try
            {
                await SaveWithConcurrency(ct);
            }
            catch (DbUpdateException ex) when (DbConstraintErrors.Translate(ex) is { } translated)
            {
                // A concurrent request won the race on a constraint (BR-28: the whole transaction was rolled back).
                throw translated;
            }

            if (_notifier != null)
            {
                await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
                {
                    AssessmentId = assessment.Id.ToString(),
                    Type = "AssessmentCompleted",
                    Status = "COMPLETED",
                    ConsumedMissionId = mission.Id.ToString(),
                    ConsumedMissionCode = mission.MissionCode,
                    ActorId = _current.UserId.ToString(),
                    ActorName = _current.Username ?? "Manager",
                    ActorRole = "MANAGER",
                    Timestamp = DateTime.UtcNow,
                    Message = $"Bản thẩm định {assessment.Id} đã hoàn tất và tạo nhiệm vụ {mission.MissionCode ?? mission.Id.ToString()}."
                }, ct);
            }

            return mission;
        }
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
            .Include(a => a.Mission)
            .Include(x => x.Towers)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == assessmentId, ct)
            ?? throw new NotFoundException("PreMissionAssessment", assessmentId);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (assessment.Status == PreMissionAssessmentStatus.Completed
            || await _db.Missions.AnyAsync(m => m.PreMissionAssessmentId == assessmentId && m.Id != missionId, ct))
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

        assessment.Status = PreMissionAssessmentStatus.Completed;
        assessment.Version++;

        Audit(assessment.Id, "ASSESSMENT_MARKED_COMPLETED");
        await SaveWithConcurrency(ct);

        if (_notifier != null)
        {
            await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
            {
                AssessmentId = assessment.Id.ToString(),
                Type = "AssessmentCompleted",
                Status = "COMPLETED",
                ConsumedMissionId = mission.Id.ToString(),
                ConsumedMissionCode = mission.MissionCode,
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Manager",
                ActorRole = "MANAGER",
                Timestamp = DateTime.UtcNow,
                Message = $"Bản thẩm định {assessment.Id} đã được đánh dấu hoàn tất cho nhiệm vụ {mission.MissionCode ?? mission.Id.ToString()}."
            }, ct);
        }

        return assessment;
    }

    public async Task<PreMissionAssessment> CancelAsync(Guid id, string? reason, CancellationToken ct)
    {
        await RequireManager(ct);

        var assessment = await _db.PreMissionAssessments
            .Include(a => a.Mission)
            .Include(x => x.Towers)
            .Include(x => x.PersonnelCandidates)
            .Include(x => x.DroneCandidates)
            .Include(x => x.Region)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PreMissionAssessment", id);

        await EnsureManagerHasAssessmentAccessAsync(assessment, ct);

        if (assessment.Status == PreMissionAssessmentStatus.Completed)
            throw new BusinessRuleException("ASSESSMENT_ALREADY_COMPLETED", "Bản đánh giá đã hoàn thành tạo nhiệm vụ bay, không thể hủy.");

        // BR-05 (Table 58): Expired and Cancelled are final. Expiry is applied first so an outdated assessment is
        // reported as Expired instead of being turned into Cancelled.
        if (assessment.Status == PreMissionAssessmentStatus.Cancelled)
            throw new BusinessRuleException("ASSESSMENT_CANCELLED", "Bản đánh giá đã bị hủy trước đó.");

        if (assessment.Status == PreMissionAssessmentStatus.Expired || await ApplyExpiryAsync(assessment, ct))
            throw new BusinessRuleException("ASSESSMENT_EXPIRED", "Bản đánh giá đã hết hạn, không thể hủy.");

        assessment.Status = PreMissionAssessmentStatus.Cancelled;
        assessment.Version++;

        Audit(assessment.Id, "ASSESSMENT_CANCELLED");
        await SaveWithConcurrency(ct);

        if (_notifier != null)
        {
            await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
            {
                AssessmentId = assessment.Id.ToString(),
                Type = "AssessmentCancelled",
                Status = "CANCELLED",
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Manager",
                ActorRole = "MANAGER",
                Timestamp = DateTime.UtcNow,
                Message = $"Bản thẩm định {assessment.Id} đã bị hủy bởi Quản lý. Lý do: {reason ?? "Không có"}"
            }, ct);
        }

        return assessment;
    }

    private async Task RequireManagerOrInspector(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var hasRole = _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) ||
                      _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) ||
                      _current.Roles.Contains(UserRoles.Inspector, StringComparer.OrdinalIgnoreCase);

        if (!hasRole)
            throw new ForbiddenException("ASSESSMENT_PERMISSION_REQUIRED");

        if (!await _db.Users.AnyAsync(x => x.Id == _current.UserId && (x.Status == "Active" || x.Status == "Enabled"), ct))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");
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

    /// <summary>Inspectors and technicians read every assessment: they inspect drones and fly for any region.
    /// Write actions are still limited by RequireManager / RequireManagerOrInspector.</summary>
    private bool IsFieldReader() =>
        _current.Roles.Contains(UserRoles.Inspector, StringComparer.OrdinalIgnoreCase) ||
        _current.Roles.Contains(UserRoles.Technician, StringComparer.OrdinalIgnoreCase) ||
        _current.Roles.Contains(UserRoles.MaintenanceTechnician, StringComparer.OrdinalIgnoreCase);

    private async Task RequireAssessmentReader(CancellationToken ct)
    {
        if (_current.Roles.Contains(UserRoles.Technician, StringComparer.OrdinalIgnoreCase) ||
            _current.Roles.Contains(UserRoles.MaintenanceTechnician, StringComparer.OrdinalIgnoreCase))
        {
            if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
                throw new ForbiddenException("AUTHENTICATION_REQUIRED");
            return;
        }

        await RequireManagerOrInspector(ct);
    }

    /// <summary>
    /// Write access (evaluate, cancel, mark completed, create mission, idempotent replay) – BR-02 / BR-14: a
    /// SystemAdmin, or a Manager who has a scope row for the assessment's region NOW. Having created the assessment
    /// is not enough once the region scope was removed.
    /// </summary>
    private async Task EnsureManagerHasAssessmentAccessAsync(PreMissionAssessment assessment, CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        if (!_current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("ASSESSMENT_ACCESS_DENIED");

        if (!await HasRegionScopeAsync(assessment.RegionId, ct))
            throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED");
    }

    /// <summary>
    /// Read access (GET one): SystemAdmin, the creator, a Manager/Inspector with the region in scope, and every
    /// Inspector or Technician (they inspect drones and fly for any region – commit 0933670).
    /// </summary>
    private async Task EnsureAssessmentReadAccessAsync(PreMissionAssessment assessment, CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) ||
            assessment.ManagerId == _current.UserId ||
            IsFieldReader())
            return;

        if (_current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) &&
            await HasRegionScopeAsync(assessment.RegionId, ct))
            return;

        throw new ForbiddenException("ASSESSMENT_ACCESS_DENIED");
    }

    /// <summary>UserGeographicScope.RegionId is non-nullable: there is no "global scope" row (BR-02).</summary>
    private Task<bool> HasRegionScopeAsync(Guid regionId, CancellationToken ct) =>
        _db.UserGeographicScopes.AnyAsync(s => s.UserId == _current.UserId && s.RegionId == regionId, ct);

    private void Audit(Guid id, string action, string oldValues = "{}", string newValues = "{}", string tableName = "PreMissionAssessments") =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = tableName,
            RecordId = id,
            ActionType = action.Length > 100 ? action[..100] : action,
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
