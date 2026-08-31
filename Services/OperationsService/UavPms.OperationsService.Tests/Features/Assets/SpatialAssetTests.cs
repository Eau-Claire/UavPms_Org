using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.SpatialAssets;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Tests.Features.AssetComponents;

public class SpatialAssetTests
{
    [Fact]
    public void Point_UsesLongitudeAsX()
    {
        var point = SpatialGeometryFactory.CreatePoint(106.80321, 10.84321);

        point.X.Should().Be(106.80321);
    }

    [Fact]
    public void Point_UsesLatitudeAsY()
    {
        var point = SpatialGeometryFactory.CreatePoint(106.80321, 10.84321);

        point.Y.Should().Be(10.84321);
    }

    [Fact]
    public void Polygon_IncludesPointInsideSelection()
    {
        var polygon = CreatePolygon();
        var inside = SpatialGeometryFactory.CreatePoint(106.805, 10.845);

        polygon.Intersects(inside).Should().BeTrue();
    }

    [Fact]
    public void Polygon_ExcludesPointOutsideSelection()
    {
        var polygon = CreatePolygon();
        var outside = SpatialGeometryFactory.CreatePoint(106.9, 10.9);

        polygon.Intersects(outside).Should().BeFalse();
    }

    [Fact]
    public async Task NearbyQuery_PassesRadiusInMetersToRepository()
    {
        var repository = new Mock<IAssetComponentRepository>();
        repository
            .Setup(x => x.GetAssetComponentsWithinDistanceAsync(10.84, 106.8, 750, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SpatialAssetMatch>());
        var handler = new NearbyAssetsQueryHandler(repository.Object);

        await handler.Handle(new NearbyAssetsQuery(10.84, 106.8, 750), CancellationToken.None);

        repository.Verify(
            x => x.GetAssetComponentsWithinDistanceAsync(10.84, 106.8, 750, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpatialQuery_InvalidPolygon_ReturnsBadRequest()
    {
        var controller = new AssetComponentController(Mock.Of<ISender>());
        var request = new SpatialQueryRequest(new GeoJsonGeometryDto(
            "Polygon",
            [[[106.8, 10.84], [106.81, 10.84], [106.8, 10.84]]]));

        var result = await controller.SpatialQuery(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static NetTopologySuite.Geometries.Polygon CreatePolygon()
    {
        var coordinates = new[]
        {
            new[]
            {
                new[] { 106.8001, 10.8401 },
                new[] { 106.8101, 10.8401 },
                new[] { 106.8101, 10.8501 },
                new[] { 106.8001, 10.8501 },
                new[] { 106.8001, 10.8401 }
            }
        };

        SpatialGeometryFactory.TryCreatePolygon("Polygon", coordinates, out var polygon, out _)
            .Should().BeTrue();
        return polygon!;
    }
}
