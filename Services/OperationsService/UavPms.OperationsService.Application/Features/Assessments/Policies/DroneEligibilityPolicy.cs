using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public sealed record DroneEligibilityResult(
    bool IsOperationallyAvailable,
    bool HasBookingConflict,
    ResourceEligibilityStatus TechnicalEligibility,
    TechnicalHealth TechnicalHealth,
    DroneTechnicalInspection? LatestInspection,
    UavScopeSuitabilityResult Scope,
    string? ReasonCode)
{
    public bool IsTechnicallyEligible => TechnicalEligibility == ResourceEligibilityStatus.Eligible;

    /// <summary>BR-20: available, no booking conflict, technically eligible and scope not Unsuitable.</summary>
    public bool IsEligible => IsOperationallyAvailable && IsTechnicallyEligible && Scope.Suitability != UavScopeSuitability.Unsuitable;
}

/// <summary>
/// BR-20 / BR-22 for one UAV. Pure: the caller passes the UAV, its inspections, the bookings and the scope,
/// so assessment evaluation, GET /drones/eligible and mission creation apply the same rules to current data.
/// </summary>
public static class DroneEligibilityPolicy
{
    /// <summary>The newest completed inspection, whatever its result: a newer Failed inspection is never hidden
    /// by an older Passed one.</summary>
    public static DroneTechnicalInspection? LatestCompletedInspection(IEnumerable<DroneTechnicalInspection> inspections, Guid droneId) =>
        inspections
            .Where(t => t.DroneId == droneId &&
                        !t.IsDeleted &&
                        t.CompletedAt.HasValue &&
                        t.Status is DroneTechnicalInspectionStatus.Passed or DroneTechnicalInspectionStatus.Failed)
            .OrderByDescending(t => t.CompletedAt)
            .FirstOrDefault();

    public static DroneEligibilityResult Evaluate(
        Uav drone,
        IEnumerable<DroneTechnicalInspection> inspections,
        IEnumerable<ResourceBooking> bookings,
        DateTime plannedStart,
        DateTime plannedEnd,
        double corridorKm,
        int towerCount)
    {
        var hasConflict = bookings.Any(b =>
            b.DroneId == drone.Id &&
            b.Status == ResourceBookingStatus.Active &&
            b.StartAt < plannedEnd &&
            b.EndAt > plannedStart);
        var isStatusAvailable = !drone.IsDeleted && drone.OperationalStatus == DroneOperationalStatus.Available;
        var isOpAvailable = isStatusAvailable && !hasConflict;

        var latest = LatestCompletedInspection(inspections, drone.Id);

        TechnicalHealth health;
        ResourceEligibilityStatus technical;
        string? technicalReason;
        if (latest == null)
        {
            // The stored health is shown, but without an inspection it never makes the UAV eligible.
            health = drone.TechnicalHealth;
            technical = health == TechnicalHealth.Critical ? ResourceEligibilityStatus.Ineligible : ResourceEligibilityStatus.RequiresInspection;
            technicalReason = health == TechnicalHealth.Critical ? "TECHNICAL_HEALTH_CRITICAL" : "NO_VALID_TECHNICAL_INSPECTION";
        }
        else if (latest.Status == DroneTechnicalInspectionStatus.Failed ||
                 latest.Health == TechnicalHealth.Critical ||
                 drone.TechnicalHealth == TechnicalHealth.Critical)
        {
            health = TechnicalHealth.Critical;
            technical = ResourceEligibilityStatus.Ineligible;
            technicalReason = "TECHNICAL_HEALTH_CRITICAL";
        }
        else if (latest.Health is not (TechnicalHealth.Healthy or TechnicalHealth.Warning))
        {
            health = latest.Health;
            technical = ResourceEligibilityStatus.Ineligible;
            technicalReason = "TECHNICAL_HEALTH_DEGRADED";
        }
        else if (!latest.ValidUntil.HasValue || latest.ValidUntil.Value <= plannedStart)
        {
            // BR-22: an inspection is valid for 7 days; it must still be valid at the planned start.
            health = latest.Health;
            technical = ResourceEligibilityStatus.RequiresInspection;
            technicalReason = "NO_VALID_TECHNICAL_INSPECTION";
        }
        else
        {
            health = latest.Health;
            technical = ResourceEligibilityStatus.Eligible;
            technicalReason = null;
        }

        var scope = UavScopeSuitabilityPolicy.Evaluate(corridorKm, towerCount, drone.FlightEnduranceMinutes, drone.OperationalRangeKm);

        string? reasonCode;
        if (!isStatusAvailable)
            reasonCode = "DRONE_NOT_AVAILABLE";
        else if (hasConflict)
            reasonCode = "SCHEDULE_CONFLICT";
        else if (technicalReason != null)
            reasonCode = technicalReason;
        else if (scope.Suitability == UavScopeSuitability.Unsuitable)
            reasonCode = "SCOPE_EXCEEDS_CAPABILITY";
        else
            reasonCode = null;

        return new DroneEligibilityResult(isOpAvailable, hasConflict, technical, health, latest, scope, reasonCode);
    }
}
