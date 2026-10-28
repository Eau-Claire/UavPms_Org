using MediatR;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Application.Features.Anomalies;

/// <summary>Towers nearest to the anomaly's GPS position (or its media's capture location), closest first.</summary>
public record GetAnomalyTowerSuggestionsQuery(Guid AnomalyId, double RadiusMeters = 300, int Limit = 5)
    : IRequest<TowerSuggestionsDto>;

public class GetAnomalyTowerSuggestionsQueryHandler : IRequestHandler<GetAnomalyTowerSuggestionsQuery, TowerSuggestionsDto>
{
    public const double MaxRadiusMeters = 5000;
    public const int MaxLimit = 20;
    private const double MetersPerDegreeLatitude = 111_320;
    private const double EarthRadiusMeters = 6_371_008.8;

    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IGenericRepository<Tower> _towerRepository;

    public GetAnomalyTowerSuggestionsQueryHandler(IAnomalyRepository anomalyRepository, IGenericRepository<Tower> towerRepository)
    {
        _anomalyRepository = anomalyRepository;
        _towerRepository = towerRepository;
    }

    public async Task<TowerSuggestionsDto> Handle(GetAnomalyTowerSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var anomaly = await _anomalyRepository.GetByIdWithDetailAsync(request.AnomalyId)
            ?? throw new NotFoundException(nameof(DetectedAnomaly), request.AnomalyId);

        string source;
        Point position;
        if (anomaly.Location is { IsEmpty: false } location)
        {
            source = "Anomaly";
            position = location;
        }
        else if (anomaly.Media?.CaptureLocation is { IsEmpty: false } capture)
        {
            source = "Media";
            position = capture as Point ?? capture.Centroid;
        }
        else
        {
            throw new BusinessRuleException("LOCATION_UNKNOWN",
                "Neither the anomaly nor its media has a GPS position; choose the tower manually.");
        }
        // Towers.Geom is SRID 4326; PostGIS rejects comparing geometries with different SRIDs.
        var origin = new Point(position.X, position.Y) { SRID = 4326 };

        var radius = Math.Clamp(request.RadiusMeters, 1, MaxRadiusMeters);
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);

        // Geometry is in degrees (SRID 4326): pre-filter with a radius in degrees that covers `radius` metres in
        // every direction (longitude degrees shrink with latitude), then rank by great-circle distance.
        var cosLat = Math.Max(Math.Cos(origin.Y * Math.PI / 180), 0.01);
        var radiusDegrees = radius / (MetersPerDegreeLatitude * cosLat);
        var candidates = await _towerRepository.FindAsync(t => t.Geom != null && t.Geom.IsWithinDistance(origin, radiusDegrees));

        var towers = candidates
            .Select(t => (Tower: t, Position: t.Geom as Point ?? t.Geom!.Centroid))
            .Select(x => (x.Tower, x.Position, Distance: HaversineMeters(origin, x.Position)))
            .Where(x => x.Distance <= radius)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .Select(x => new TowerSuggestionDto(
                x.Tower.Id, x.Tower.TowerCode, x.Tower.RegionId, x.Tower.Address,
                x.Position.Y, x.Position.X, Math.Round(x.Distance, 1), x.Tower.Id == anomaly.TowerId))
            .ToList();

        return new TowerSuggestionsDto(anomaly.Id, anomaly.TowerId, source, origin.Y, origin.X, radius, towers);
    }

    private static double HaversineMeters(Point a, Point b)
    {
        double ToRad(double deg) => deg * Math.PI / 180;
        var dLat = ToRad(b.Y - a.Y);
        var dLon = ToRad(b.X - a.X);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(a.Y)) * Math.Cos(ToRad(b.Y)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}
