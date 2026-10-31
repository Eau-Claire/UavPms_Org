using System.Globalization;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Re-validation of people and UAVs when they are put on a mission (create, add member, reassign, change UAV),
/// plus the UAV and booking bookkeeping shared by the mission lifecycle (BR-18 – BR-20, BR-26, BR-32, BR-45).
/// Booking queries read ResourceBookings directly: a filtered Include of the (soft-deleted) mission would hide
/// bookings that the database exclusion constraint still enforces.
/// </summary>
internal static class MissionResourceGuard
{
    public const int DefaultConfirmationDeadlineHours = 24;
    public const int DefaultStartToleranceMinutes = 120;

    private static readonly string[] NonFieldRoles = { UserRoles.SystemAdmin, UserRoles.Manager };

    /// <summary>BR-19: an Active booking of the user overlapping [start, end) in another mission.</summary>
    public static async Task EnsureUserNotBookedAsync(ApplicationDbContext db, Guid userId, DateTime start, DateTime end, Guid? excludeMissionId, CancellationToken ct)
    {
        var conflict = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.Status == ResourceBookingStatus.Active && b.StartAt < end && b.EndAt > start)
            .Where(b => excludeMissionId == null || b.MissionId != excludeMissionId.Value)
            .OrderBy(b => b.StartAt)
            .FirstOrDefaultAsync(ct);
        if (conflict != null) await ThrowConflictAsync(db, conflict, $"Person {userId}", ct);
    }

    /// <summary>BR-19: an Active booking of the UAV overlapping [start, end) in another mission.</summary>
    public static async Task EnsureDroneNotBookedAsync(ApplicationDbContext db, Guid droneId, DateTime start, DateTime end, Guid? excludeMissionId, CancellationToken ct)
    {
        var conflict = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.DroneId == droneId && b.Status == ResourceBookingStatus.Active && b.StartAt < end && b.EndAt > start)
            .Where(b => excludeMissionId == null || b.MissionId != excludeMissionId.Value)
            .OrderBy(b => b.StartAt)
            .FirstOrDefaultAsync(ct);
        if (conflict != null) await ThrowConflictAsync(db, conflict, $"UAV {droneId}", ct);
    }

    private static async Task ThrowConflictAsync(ApplicationDbContext db, ResourceBooking booking, string resource, CancellationToken ct)
    {
        var code = await db.Missions.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Id == booking.MissionId)
            .Select(m => m.MissionCode)
            .FirstOrDefaultAsync(ct);
        throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
            $"{resource} is booked in mission {code ?? booking.MissionId.ToString()} ({booking.StartAt:yyyy-MM-dd HH:mm} - {booking.EndAt:yyyy-MM-dd HH:mm} UTC).");
    }

    /// <summary>
    /// BR-18 at assignment time: Active and verified, a field role and no Manager/SystemAdmin role, in scope for the
    /// region (as in the assessment evaluation, a SystemAdmin caller puts everyone in scope) and not booked.
    /// </summary>
    public static async Task<User> EnsurePersonEligibleAsync(
        ApplicationDbContext db, Guid userId, Guid regionId, DateTime start, DateTime end, Guid? excludeMissionId, bool callerIsGlobal, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (!IsActive(user.Status) || !user.IsEmailVerified)
            throw new BusinessRuleException("ASSIGNEE_INACTIVE", $"User {userId} is not active or not verified.");

        var roleNames = user.UserRoles.Where(r => r.Role != null).Select(r => r.Role!.RoleName).ToList();
        if (roleNames.Any(r => NonFieldRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            throw new BusinessRuleException("ASSIGNEE_ROLE_NOT_ELIGIBLE", $"User {userId} has a Manager or SystemAdmin role.");

        await EnsureUserNotBookedAsync(db, userId, start, end, excludeMissionId, ct);

        var scopes = await db.UserGeographicScopes.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.Status == ResourceBookingStatus.Active && b.StartAt < end && b.EndAt > start)
            .Where(b => excludeMissionId == null || b.MissionId != excludeMissionId.Value)
            .ToListAsync(ct);

        var evaluation = PersonnelEligibilityPolicy.EvaluatePersonnel(user, regionId, start, end, scopes, bookings, callerIsGlobal);
        if (!evaluation.IsEligible)
        {
            var (code, message) = evaluation.ReasonCode switch
            {
                "USER_INACTIVE" => ("ASSIGNEE_INACTIVE", "is not active or not verified"),
                "INVALID_ROLE" => ("ASSIGNEE_ROLE_NOT_ELIGIBLE", "has no field role"),
                "OUTSIDE_MANAGEMENT_SCOPE" => ("ASSIGNEE_OUT_OF_SCOPE", "is not in scope for the mission region"),
                "SCHEDULE_CONFLICT" => ("RESOURCE_BOOKING_CONFLICT", "is booked in an overlapping window"),
                _ => ("ASSIGNEE_NOT_ELIGIBLE", $"is not eligible ({evaluation.ReasonCode})"),
            };
            throw new BusinessRuleException(code, $"User {userId} {message}.");
        }

        return user;
    }

    /// <summary>
    /// BR-20/BR-26 at assignment time: Available, health Healthy or Warning, not Unsuitable for the assessed scope
    /// (when the UAV was a candidate of the assessment) and not booked.
    /// </summary>
    public static async Task<Uav> EnsureDroneEligibleAsync(
        ApplicationDbContext db, Guid droneId, DateTime start, DateTime end, Guid? excludeMissionId, Guid? assessmentId, CancellationToken ct)
    {
        var drone = await db.Uavs.SingleOrDefaultAsync(d => d.Id == droneId, ct)
            ?? throw new NotFoundException("Drone", droneId);

        if (drone.OperationalStatus != DroneOperationalStatus.Available)
            throw new BusinessRuleException("DRONE_NOT_AVAILABLE", $"UAV {drone.UavCode} is {drone.OperationalStatus}, not Available.");
        if (drone.TechnicalHealth == TechnicalHealth.Critical)
            throw new BusinessRuleException("DRONE_CRITICAL_HEALTH", $"UAV {drone.UavCode} has Critical technical health.");
        if (drone.TechnicalHealth is not (TechnicalHealth.Healthy or TechnicalHealth.Warning))
            throw new BusinessRuleException("DRONE_HEALTH_UNKNOWN", $"UAV {drone.UavCode} has no passed technical inspection (health {drone.TechnicalHealth}).");

        if (assessmentId.HasValue)
        {
            var suitability = await db.PreMissionAssessmentDrones.AsNoTracking()
                .Where(d => d.AssessmentId == assessmentId.Value && d.DroneId == droneId)
                .Select(d => (UavScopeSuitability?)d.Suitability)
                .FirstOrDefaultAsync(ct);
            if (suitability == UavScopeSuitability.Unsuitable)
                throw new BusinessRuleException("DRONE_UNSUITABLE", $"UAV {drone.UavCode} is Unsuitable for the mission scope.");
        }

        await EnsureDroneNotBookedAsync(db, droneId, start, end, excludeMissionId, ct);
        return drone;
    }

    /// <summary>True when another In Progress mission flies this UAV.</summary>
    public static Task<bool> UavFlyingInOtherMissionAsync(ApplicationDbContext db, Guid droneId, Guid missionId, CancellationToken ct) =>
        db.Missions.AnyAsync(m => m.Id != missionId && m.UavId == droneId && m.Status == MissionStatus.InProgress, ct);

    /// <summary>
    /// Sets the UAV back to Idle after its mission ends (complete, cancel, return), unless another In Progress
    /// mission still flies it.
    /// </summary>
    public static async Task ReleaseUavAsync(ApplicationDbContext db, Guid? droneId, Guid missionId, CancellationToken ct)
    {
        if (!droneId.HasValue || droneId.Value == Guid.Empty) return;
        var drone = await db.Uavs.FirstOrDefaultAsync(d => d.Id == droneId.Value, ct);
        if (drone == null || drone.FlightStatus != DroneStatus.Flying) return;
        if (await UavFlyingInOtherMissionAsync(db, drone.Id, missionId, ct)) return;
        drone.FlightStatus = DroneStatus.Idle;
        drone.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Sets every Active booking of the mission to the given status.</summary>
    public static async Task EndActiveBookingsAsync(ApplicationDbContext db, Guid missionId, ResourceBookingStatus status, CancellationToken ct, Func<ResourceBooking, bool>? filter = null)
    {
        var bookings = await db.ResourceBookings
            .Where(b => b.MissionId == missionId && b.Status == ResourceBookingStatus.Active)
            .ToListAsync(ct);
        foreach (var b in bookings.Where(b => filter == null || filter(b)))
        {
            b.Status = status;
            b.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>BR-09: integer system setting read without the settings service (the lifecycle services are built without DI in tests).</summary>
    public static async Task<int> GetIntSettingAsync(ApplicationDbContext db, string key, int defaultValue, CancellationToken ct)
    {
        var raw = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : defaultValue;
    }

    public static bool IsActive(string? status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Enabled", StringComparison.OrdinalIgnoreCase);
}
