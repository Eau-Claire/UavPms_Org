using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public static class AssessmentExpiryPolicy
{
    public static bool CheckAndApplyExpiry(PreMissionAssessment assessment)
    {
        if (assessment.Status is PreMissionAssessmentStatus.Ready or PreMissionAssessmentStatus.Draft or PreMissionAssessmentStatus.Evaluating or PreMissionAssessmentStatus.NotReady)
        {
            var isExpired = (assessment.ValidUntil.HasValue && assessment.ValidUntil.Value <= DateTime.UtcNow) ||
                            (assessment.PlannedEnd <= DateTime.UtcNow);

            if (isExpired)
            {
                assessment.Status = PreMissionAssessmentStatus.Expired;
                assessment.Version++;
                return true;
            }
        }

        return false;
    }
}
