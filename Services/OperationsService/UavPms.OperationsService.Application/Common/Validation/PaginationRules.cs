using FluentValidation;
using System.Linq.Expressions;

namespace UavPms.OperationsService.Application.Common.Validation;

public static class PaginationRules
{
    public const int MaximumPageSize = 100;

    public static void Apply<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int>> page,
        Expression<Func<T, int>> pageSize)
    {
        validator.RuleFor(page).GreaterThan(0);
        validator.RuleFor(pageSize).InclusiveBetween(1, MaximumPageSize);
    }
}
