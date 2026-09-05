using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;

public sealed class ListMissionsQueryValidator : AbstractValidator<ListMissionsQuery>
{
    public ListMissionsQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
