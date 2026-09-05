using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Towers.Queries.GetTowers;

public sealed class GetTowersQueryValidator : AbstractValidator<GetTowersQuery>
{
    public GetTowersQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
