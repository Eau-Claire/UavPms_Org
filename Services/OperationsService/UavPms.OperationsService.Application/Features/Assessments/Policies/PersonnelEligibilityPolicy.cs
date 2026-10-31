using System.Text.Json;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

/// <summary>
/// BR-18 / BR-19: decides whether one person may be assigned for a region and planned window.
/// Pure: every input is current data passed in by the caller, so assessment evaluation and mission creation
/// can both re-check a candidate with the same rules.
/// </summary>
public static class PersonnelEligibilityPolicy
{
    private static readonly string[] AllowedOperationalRoles = new[]
    {
        UserRoles.Inspector,
        UserRoles.Analyst,
        UserRoles.Technician,
        UserRoles.MaintenanceTechnician,
        "Pilot"
    };

    private static readonly string[] NonOperationalRoles = new[]
    {
        UserRoles.SystemAdmin,
        UserRoles.Manager
    };

    public static IReadOnlyList<string> FieldRoleNames => AllowedOperationalRoles;
    public static IReadOnlyList<string> NonFieldRoleNames => NonOperationalRoles;

    /// <param name="isGlobalAdmin">
    /// Ignored. It used to receive the CALLER's SystemAdmin flag, which put every field user in scope when an
    /// administrator evaluated (BR-18). A candidate's scope comes only from the candidate's own scope rows.
    /// Kept so existing callers still compile.
    /// </param>
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
            else if (primaryRole.Equals(UserRoles.Technician, StringComparison.OrdinalIgnoreCase) || primaryRole.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase))
                primaryRole = UserRoles.Technician;
        }

        var hasNonOperationalRole = roleNames.Any(r => NonOperationalRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
        var hasValidRole = primaryRole != null && !hasNonOperationalRole;

        var isActive = !user.IsDeleted && user.IsEmailVerified && (user.Status == "Active" || user.Status == "Enabled");

        // UserGeographicScope.RegionId is non-nullable: there is no "global scope" row (BR-02).
        var inScope = userScopes.Any(s => s.UserId == user.Id && s.RegionId == regionId && !s.IsDeleted);

        var hasBookingConflict = userBookings.Any(b =>
            b.UserId == user.Id &&
            b.Status == ResourceBookingStatus.Active &&
            b.StartAt < plannedEnd &&
            b.EndAt > plannedStart);

        var eligibilityStatus = (hasValidRole && isActive && inScope)
            ? ResourceEligibilityStatus.Eligible
            : ResourceEligibilityStatus.Ineligible;

        var availabilityStatus = !hasBookingConflict
            ? ResourceAvailabilityStatus.Available
            : ResourceAvailabilityStatus.Unavailable;

        // Same order as the criteria table of UC-MF01-03: account, role, scope, schedule.
        string? reasonCode = null;
        if (!isActive)
            reasonCode = "USER_INACTIVE";
        else if (!hasValidRole)
            reasonCode = "INVALID_ROLE";
        else if (!inScope)
            reasonCode = "OUTSIDE_MANAGEMENT_SCOPE";
        else if (hasBookingConflict)
            reasonCode = "SCHEDULE_CONFLICT";

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
            EligibilityStatus = eligibilityStatus,
            AvailabilityStatus = availabilityStatus,
            ReasonCode = reasonCode,
            SnapshotAt = DateTime.UtcNow,
            Findings = JsonSerializer.Serialize(findings)
        };
    }
}
