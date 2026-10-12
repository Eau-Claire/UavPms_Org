using System.Text;
using FluentAssertions;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class SiteFeasibilityPolicyTests
{
    [Fact]
    public void ParseBoundary_ValidPolygon_ReturnsGeometryWithSrid4326()
    {
        var wkt = "POLYGON((105.8 21.0, 105.9 21.0, 105.9 21.1, 105.8 21.1, 105.8 21.0))";
        var geom = SiteFeasibilityPolicy.ParseBoundary(wkt);

        geom.Should().NotBeNull();
        geom.SRID.Should().Be(4326);
        geom.IsValid.Should().BeTrue();
        (geom is Polygon).Should().BeTrue();
    }

    [Fact]
    public void ParseBoundary_EmptyOrWhitespace_ThrowsBusinessRuleException()
    {
        var actNull = () => SiteFeasibilityPolicy.ParseBoundary("");
        var actWhitespace = () => SiteFeasibilityPolicy.ParseBoundary("   ");

        actNull.Should().Throw<BusinessRuleException>()
            .WithMessage("*INVALID_GEOMETRY*");
        actWhitespace.Should().Throw<BusinessRuleException>()
            .WithMessage("*INVALID_GEOMETRY*");
    }

    [Fact]
    public void ParseBoundary_ExceedsMaxLength_ThrowsBusinessRuleException()
    {
        var hugeWkt = new string('A', SiteFeasibilityPolicy.MaxWktLength + 1);
        var act = () => SiteFeasibilityPolicy.ParseBoundary(hugeWkt);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*INVALID_GEOMETRY*")
            .WithMessage($"*{SiteFeasibilityPolicy.MaxWktLength}*");
    }

    [Fact]
    public void ParseBoundary_ExceedsMaxVertices_ThrowsBusinessRuleException()
    {
        // Build a polygon with 1005 vertices
        var sb = new StringBuilder();
        sb.Append("POLYGON((");
        int vertexCount = 1005;
        for (int i = 0; i < vertexCount - 1; i++)
        {
            double angle = (2 * Math.PI * i) / (vertexCount - 1);
            double lon = 105.0 + Math.Cos(angle) * 0.1;
            double lat = 21.0 + Math.Sin(angle) * 0.1;
            sb.Append($"{lon:F6} {lat:F6},");
        }
        // Close polygon ring
        double firstAngle = 0;
        double firstLon = 105.0 + Math.Cos(firstAngle) * 0.1;
        double firstLat = 21.0 + Math.Sin(firstAngle) * 0.1;
        sb.Append($"{firstLon:F6} {firstLat:F6}))");

        var wkt = sb.ToString();
        var act = () => SiteFeasibilityPolicy.ParseBoundary(wkt);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*INVALID_GEOMETRY*")
            .WithMessage($"*{SiteFeasibilityPolicy.MaxVertices}*");
    }

    [Fact]
    public void ParseBoundary_InvalidGeometryType_ThrowsBusinessRuleException()
    {
        var pointWkt = "POINT(105.8 21.0)";
        var act = () => SiteFeasibilityPolicy.ParseBoundary(pointWkt);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*INVALID_GEOMETRY*");
    }
}
