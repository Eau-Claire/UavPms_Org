using MediatR;

namespace UavPms.OperationsService.Application.Features.Gis.Infrastructure;

/// <summary>Bản đồ hạ tầng: khu vực → cột điện, kèm bất thường và cảnh báo đang mở.</summary>
public record GisInfrastructureQuery(Guid? AdministrativeAreaId) : IRequest<GisInfrastructureResponse>;

public record GisInfrastructureResponse(IReadOnlyList<GisTowerDto> Towers)
{
    public IReadOnlyList<GisAnomalyDto> Anomalies { get; init; } = [];
    public IReadOnlyList<GisAlertDto> Alerts { get; init; } = [];
}
public record GisTowerDto(Guid Id, string Code, Guid RegionId, string? PoleType, string? Address, double Latitude, double Longitude);
public record GisAnomalyDto(Guid Id, Guid AnomalyId, Guid? TowerId, string TowerCode, string Category, double Severity, double Latitude, double Longitude, string Status, double ConfidenceScore, string? ImageUrl, DateTime DetectedAt);
public record GisAlertDto(Guid Id, Guid AnomalyId, Guid? TowerId, string TowerCode, double Latitude, double Longitude, string Status, string Priority, string Title, DateTime TriggeredAt);
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
