using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Regions.Queries.GetRegions;

public sealed class GetRegionsQueryValidator : AbstractValidator<GetRegionsQuery>
{
    public GetRegionsQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
