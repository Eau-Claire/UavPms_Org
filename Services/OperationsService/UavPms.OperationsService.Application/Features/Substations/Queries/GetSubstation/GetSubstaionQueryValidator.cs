using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Substations.Queries.GetSubstation;

public sealed class GetSubstaionQueryValidator : AbstractValidator<GetSubstaionQuery>
{
    public GetSubstaionQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
