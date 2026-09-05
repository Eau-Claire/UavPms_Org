using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetRecentDefects;

public sealed class GetRecentDefectsQueryValidator : AbstractValidator<GetRecentDefectsQuery>
{
    public GetRecentDefectsQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
