using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetInspectionHistory;

public sealed class GetInspectionHistoryQueryValidator : AbstractValidator<GetInspectionHistoryQuery>
{
    public GetInspectionHistoryQueryValidator()
    {
        PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
        RuleFor(query => query)
            .Must(query => !query.FromDate.HasValue || !query.ToDate.HasValue || query.FromDate <= query.ToDate)
            .WithMessage("FromDate must be earlier than or equal to ToDate.");
    }
}
