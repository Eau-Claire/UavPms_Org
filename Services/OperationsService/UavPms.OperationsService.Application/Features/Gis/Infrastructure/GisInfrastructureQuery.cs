using MediatR;

namespace UavPms.OperationsService.Application.Features.Gis.Infrastructure;

public record GisInfrastructureQuery(Guid? AdministrativeAreaId, Guid? ManagementUnitId, Guid? PowerLineId,
    string? VoltageLevel, string? AssetType, string? Status) : IRequest<GisInfrastructureResponse>;

public record GisInfrastructureResponse(IReadOnlyList<GisPowerLineDto> PowerLines,
    IReadOnlyList<GisLineSegmentDto> LineSegments, IReadOnlyList<GisAssetDto> Assets)
{
    public IReadOnlyList<GisAnomalyDto> Anomalies { get; init; } = [];
    public IReadOnlyList<GisAlertDto> Alerts { get; init; } = [];
}
public record GisAnomalyDto(Guid Id, Guid AnomalyId, string AssetCode, string TowerCode, string Category, double Severity, double Latitude, double Longitude, string Status, double ConfidenceScore, string? ImageUrl, DateTime DetectedAt);
public record GisAlertDto(Guid Id, Guid AnomalyId, string AssetCode, string TowerCode, double Latitude, double Longitude, string Status, string Priority, string Title, DateTime TriggeredAt);
public record GisPowerLineDto(Guid Id, string Code, string Name, string VoltageLevel, Guid? ManagementUnitId, string Status, string? Geometry);
public record GisLineSegmentDto(Guid Id, Guid PowerLineId, Guid FromAssetId, Guid ToAssetId, int Sequence, string Status, string? Geometry);
public record GisAssetDto(Guid Id, string Code, string AssetType, string Status, Guid? ManagementUnitId, Guid? PowerLineId, double Latitude, double Longitude);
public interface IGisRepository
{
    Task<GisInfrastructureResponse> GetInfrastructureAsync(GisInfrastructureQuery query, CancellationToken cancellationToken);
}

public class GisInfrastructureQueryHandler : IRequestHandler<GisInfrastructureQuery, GisInfrastructureResponse>
{
    private readonly IGisRepository _repository;
    public GisInfrastructureQueryHandler(IGisRepository repository) => _repository = repository;
    public Task<GisInfrastructureResponse> Handle(GisInfrastructureQuery request, CancellationToken cancellationToken)
        => _repository.GetInfrastructureAsync(request, cancellationToken);
}
