using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

public sealed class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "healthScore", "riskLevel", "lastInspectedAt", "assetCode"
    };

    private static readonly HashSet<string> AllowedSortOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc", "desc"
    };

    public GetAssetsQueryValidator()
    {
        PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
        RuleFor(query => query)
            .Must(query => !query.MinHealthScore.HasValue || !query.MaxHealthScore.HasValue || query.MinHealthScore <= query.MaxHealthScore)
            .WithMessage("MinHealthScore must be less than or equal to MaxHealthScore.");
        RuleFor(query => query.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || AllowedSortFields.Contains(sortBy.Trim()))
            .WithMessage("SortBy must be one of: healthScore, riskLevel, lastInspectedAt, assetCode.");
        RuleFor(query => query.SortOrder)
            .Must(sortOrder => string.IsNullOrWhiteSpace(sortOrder) || AllowedSortOrders.Contains(sortOrder.Trim()))
            .WithMessage("SortOrder must be either asc or desc.");
    }
}
