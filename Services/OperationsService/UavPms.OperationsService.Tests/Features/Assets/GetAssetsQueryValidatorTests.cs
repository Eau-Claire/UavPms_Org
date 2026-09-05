using FluentAssertions;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

namespace UavPms.OperationsService.Tests.Features.Assets;

public sealed class GetAssetsQueryValidatorTests
{
    private readonly GetAssetsQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_RejectsInvalidPagination(int page, int pageSize)
    {
        var result = _validator.Validate(new GetAssetsQuery(Page: page, PageSize: pageSize));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsInvertedHealthScoreRange()
    {
        var result = _validator.Validate(new GetAssetsQuery(MinHealthScore: 90, MaxHealthScore: 10));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsSupportedFilters()
    {
        var result = _validator.Validate(new GetAssetsQuery(Page: 1, PageSize: 100, MinHealthScore: 10, MaxHealthScore: 90));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("unknown", "asc")]
    [InlineData("healthScore", "sideways")]
    public void Validate_RejectsUnsupportedSortParameters(string sortBy, string sortOrder)
    {
        var result = _validator.Validate(new GetAssetsQuery(SortBy: sortBy, SortOrder: sortOrder));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsSupportedSortParameters()
    {
        var result = _validator.Validate(new GetAssetsQuery(SortBy: "healthScore", SortOrder: "DESC"));

        result.IsValid.Should().BeTrue();
    }
}
