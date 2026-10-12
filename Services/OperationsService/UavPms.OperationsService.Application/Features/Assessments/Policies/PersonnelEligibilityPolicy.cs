using System.Text.Json;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public static class PersonnelEligibilityPolicy
{
    private static readonly string[] AllowedOperationalRoles = new[]
    {
        UserRoles.Inspector,
        UserRoles.Analyst,
        "Technician",
        UserRoles.MaintenanceTechnician,
        "Pilot"
    };

    public static PreMissionAssessmentPersonnel EvaluatePersonnel(
        User user,
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyList<UserGeographicScope> userScopes,
        IReadOnlyList<ResourceBooking> userBookings,
        bool isGlobalAdmin = false)
    {
        var roleNames = user.UserRoles
            .Where(r => r.Role != null)
            .Select(r => r.Role!.RoleName)
            .ToList();

        var primaryRole = roleNames.FirstOrDefault(r => AllowedOperationalRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
        if (primaryRole != null)
        {
            if (primaryRole.Equals(UserRoles.Inspector, StringComparison.OrdinalIgnoreCase) || primaryRole.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
                primaryRole = UserRoles.Inspector;
            else if (primaryRole.Equals(UserRoles.Analyst, StringComparison.OrdinalIgnoreCase))
                primaryRole = UserRoles.Analyst;
            else if (primaryRole.Equals("Technician", StringComparison.OrdinalIgnoreCase) || primaryRole.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase))
                primaryRole = "Technician";
        }
        var hasValidRole = primaryRole != null;

        var isActive = user.IsEmailVerified && (user.Status == "Active" || user.Status == "Enabled");

        var inScope = isGlobalAdmin || userScopes.Any(s => s.UserId == user.Id && (s.RegionId == regionId || s.RegionId == null));

        var hasBookingConflict = userBookings.Any(b =>
            b.UserId == user.Id &&
            b.Status == ResourceBookingStatus.Active &&
            b.StartAt < plannedEnd &&
            b.EndAt > plannedStart);

        var isEligible = hasValidRole && isActive && inScope && !hasBookingConflict;
        var eligibilityStatus = (hasValidRole && isActive && inScope)
            ? ResourceEligibilityStatus.Eligible
            : ResourceEligibilityStatus.Ineligible;

        var availabilityStatus = !hasBookingConflict
            ? ResourceAvailabilityStatus.Available
            : ResourceAvailabilityStatus.Unavailable;

        string? reasonCode = null;
        if (!inScope)
            reasonCode = "OUTSIDE_MANAGEMENT_SCOPE";
        else if (hasBookingConflict)
            reasonCode = "SCHEDULE_CONFLICT";
        else if (!hasValidRole)
            reasonCode = "INVALID_ROLE";
        else if (!isActive)
            reasonCode = "USER_INACTIVE";

        var findings = new Dictionary<string, object>
        {
            ["userId"] = user.Id,
            ["fullName"] = user.FullName ?? string.Empty,
            ["role"] = primaryRole ?? UserRoles.Inspector,
            ["hasInspectorRole"] = roleNames.Any(r => r.Equals(UserRoles.Inspector, StringComparison.OrdinalIgnoreCase)),
            ["hasValidRole"] = hasValidRole,
            ["isActive"] = isActive,
            ["inScope"] = inScope,
            ["hasBookingConflict"] = hasBookingConflict
        };

        return new PreMissionAssessmentPersonnel
        {
            UserId = user.Id,
            User = user,
            IsEligible = isEligible,
            EligibilityStatus = eligibilityStatus,
            AvailabilityStatus = availabilityStatus,
            ReasonCode = reasonCode,
            SnapshotAt = DateTime.UtcNow,
            Findings = JsonSerializer.Serialize(findings)
        };
    }
}
