using System.Text.Json;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public static class PersonnelEligibilityPolicy
{
    public static PreMissionAssessmentPersonnel EvaluatePersonnel(
        User user,
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyList<UserGeographicScope> userScopes,
        IReadOnlyList<ResourceBooking> userBookings,
        bool isGlobalAdmin = false)
    {
        var findings = new Dictionary<string, object>
        {
            ["userId"] = user.Id,
            ["fullName"] = user.FullName ?? string.Empty
        };

        var hasInspectorRole = user.UserRoles.Any(r => r.Role != null && r.Role.RoleName == UserRoles.Inspector);
        findings["hasInspectorRole"] = hasInspectorRole;

        var isActive = user.IsEmailVerified && (user.Status == "Active" || user.Status == "Enabled");
        findings["isActive"] = isActive;

        var inScope = isGlobalAdmin || userScopes.Any(s => s.UserId == user.Id && (s.RegionId == regionId || s.RegionId == null));
        findings["inScope"] = inScope;

        var hasBookingConflict = userBookings.Any(b =>
            b.UserId == user.Id &&
            b.Status == ResourceBookingStatus.Active &&
            b.StartAt < plannedEnd &&
            b.EndAt > plannedStart);
        findings["hasBookingConflict"] = hasBookingConflict;

        var isEligible = hasInspectorRole && isActive && inScope && !hasBookingConflict;
        var eligibilityStatus = (hasInspectorRole && isActive && inScope)
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
        else if (!hasInspectorRole)
            reasonCode = "INVALID_ROLE";
        else if (!isActive)
            reasonCode = "USER_INACTIVE";

        return new PreMissionAssessmentPersonnel
        {
            UserId = user.Id,
            IsEligible = isEligible,
            EligibilityStatus = eligibilityStatus,
            AvailabilityStatus = availabilityStatus,
            ReasonCode = reasonCode,
            SnapshotAt = DateTime.UtcNow,
            Findings = JsonSerializer.Serialize(findings)
        };
    }
}
