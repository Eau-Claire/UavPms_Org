using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

public sealed class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
    public GetAssetsQueryValidator()
    {
        PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
        RuleFor(query => query)
            .Must(query => !query.MinHealthScore.HasValue || !query.MaxHealthScore.HasValue || query.MinHealthScore <= query.MaxHealthScore)
            .WithMessage("MinHealthScore must be less than or equal to MaxHealthScore.");
    }
}
