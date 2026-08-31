using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace UavPms.OperationsService.Application.Common.Utilities;

public static class SpatialGeometryFactory
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static Point CreatePoint(double longitude, double latitude) =>
        GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));

    public static bool TryCreatePolygon(
        string? type,
        double[][][]? coordinates,
        out Polygon? polygon,
        out string error)
    {
        polygon = null;
        error = string.Empty;

        if (!string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            error = "Geometry type must be Polygon.";
            return false;
        }

        if (coordinates is not { Length: > 0 })
        {
            error = "Polygon coordinates are required.";
            return false;
        }

        var rings = new LinearRing[coordinates.Length];
        for (var ringIndex = 0; ringIndex < coordinates.Length; ringIndex++)
        {
            var positions = coordinates[ringIndex];
            if (positions is not { Length: >= 4 })
            {
                error = "Each polygon ring must contain at least four positions.";
                return false;
            }

            var ringCoordinates = new Coordinate[positions.Length];
            for (var positionIndex = 0; positionIndex < positions.Length; positionIndex++)
            {
                var position = positions[positionIndex];
                if (position is not { Length: 2 }
                    || !IsValidLongitude(position[0])
                    || !IsValidLatitude(position[1]))
                {
                    error = "Each position must contain a valid longitude and latitude.";
                    return false;
                }

                ringCoordinates[positionIndex] = new Coordinate(position[0], position[1]);
            }

            if (!ringCoordinates[0].Equals2D(ringCoordinates[^1]))
            {
                error = "Each polygon ring must be closed.";
                return false;
            }

            rings[ringIndex] = GeometryFactory.CreateLinearRing(ringCoordinates);
        }

        polygon = GeometryFactory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
        if (!polygon.IsValid || polygon.IsEmpty)
        {
            polygon = null;
            error = "Polygon geometry is malformed.";
            return false;
        }

        return true;
    }

    public static bool IsValidLatitude(double latitude) =>
        double.IsFinite(latitude) && latitude is >= -90 and <= 90;

    public static bool IsValidLongitude(double longitude) =>
        double.IsFinite(longitude) && longitude is >= -180 and <= 180;
}
