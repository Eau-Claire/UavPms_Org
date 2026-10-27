using FluentValidation;
using MediatR;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Validation;
using UavPms.OperationsService.Application.Features.Towers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Queries;

public record GetTowersQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? RegionId = null,
    string? Search = null) : IRequest<PaginatedTowersResponse>;

public record GetTowerByIdQuery(Guid Id) : IRequest<TowerDto>;

/// <summary>Trang chi tiết cột điện: bất thường (bỏ bản đã bị thay thế) và lịch sử bảo trì.</summary>
public record GetTowerHistoryQuery(Guid Id) : IRequest<TowerHistoryDto>;

public record SpatialTowerQuery(Polygon Polygon, Guid? RegionId) : IRequest<SpatialTowerQueryResponse>;

public sealed class GetTowersQueryValidator : AbstractValidator<GetTowersQuery>
{
    public GetTowersQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}

public class GetTowersQueryHandler : IRequestHandler<GetTowersQuery, PaginatedTowersResponse>
{
    private readonly ITowerRepository _towerRepository;

    public GetTowersQueryHandler(ITowerRepository towerRepository) => _towerRepository = towerRepository;

    public async Task<PaginatedTowersResponse> Handle(GetTowersQuery request, CancellationToken cancellationToken)
    {
        var (towers, totalCount) = await _towerRepository.GetTowersPagedAsync(
            request.Page, request.PageSize, request.RegionId, request.Search);

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        return new PaginatedTowersResponse(
            towers.Select(TowerDto.From).ToList(),
            new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages));
    }
}

public class GetTowerByIdQueryHandler : IRequestHandler<GetTowerByIdQuery, TowerDto>
{
    private readonly ITowerRepository _towerRepository;

    public GetTowerByIdQueryHandler(ITowerRepository towerRepository) => _towerRepository = towerRepository;

    public async Task<TowerDto> Handle(GetTowerByIdQuery request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetAccessibleByIdAsync(request.Id, cancellationToken);
        return tower == null ? throw new NotFoundException("Tower", request.Id) : TowerDto.From(tower);
    }
}

public class GetTowerHistoryQueryHandler : IRequestHandler<GetTowerHistoryQuery, TowerHistoryDto>
{
    private readonly ITowerRepository _towerRepository;
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;

    public GetTowerHistoryQueryHandler(
        ITowerRepository towerRepository,
        IAnomalyRepository anomalyRepository,
        IMaintenanceTicketRepository ticketRepository)
    {
        _towerRepository = towerRepository;
        _anomalyRepository = anomalyRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<TowerHistoryDto> Handle(GetTowerHistoryQuery request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetAccessibleByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Tower", request.Id);

        var anomalies = await _anomalyRepository.GetByTowerIdWithDetailsAsync(tower.Id);
        var tickets = await _ticketRepository.GetByTowerIdWithDetailsAsync(tower.Id);

        return new TowerHistoryDto(
            TowerDto.From(tower),
            anomalies.Select(a => new TowerAnomalyDto(
                a.Id,
                a.MediaId,
                a.Media?.MissionId ?? Guid.Empty,
                a.Category?.CategoryCode,
                a.Category?.CategoryName,
                a.ConfidenceScore,
                a.ValidationStatus,
                a.AnalysisRun?.ModelName ?? DetectedAnomaly.ManualSource,
                a.ImageUrl,
                a.CropUrl,
                a.CreatedAt,
                a.ValidatedAt)).ToList(),
            tickets.Select(t => new TowerMaintenanceDto(
                t.Id,
                t.TicketCode,
                t.AnomalyId,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.Description,
                t.ManagerId,
                t.TechnicianId,
                t.CreatedAt,
                t.DueDate,
                t.StartedAt,
                t.ResolvedAt,
                t.MaintenanceProofs.Where(p => !p.IsDeleted)
                    .Select(p => new TowerMaintenanceProofDto(p.Id, p.ProofType, p.FileUrl, p.TechnicianNotes, p.UploadedAt)).ToList(),
                t.MaterialLogs.Where(m => !m.IsDeleted)
                    .Select(m => new TowerMaterialLogDto(m.Id, m.ComponentCode, m.ComponentName, m.QuantityUsed, m.Unit, m.LoggedAt)).ToList()))
                .ToList());
    }
}

public class SpatialTowerQueryHandler : IRequestHandler<SpatialTowerQuery, SpatialTowerQueryResponse>
{
    private readonly ITowerRepository _towerRepository;

    public SpatialTowerQueryHandler(ITowerRepository towerRepository) => _towerRepository = towerRepository;

    public async Task<SpatialTowerQueryResponse> Handle(SpatialTowerQuery request, CancellationToken cancellationToken)
    {
        var towers = await _towerRepository.GetTowersIntersectingAsync(request.Polygon, request.RegionId, cancellationToken);
        var dtos = towers
            .Where(t => t.Geom != null)
            .Select(t => new SpatialTowerDto(t.Id, t.TowerCode, t.RegionId, t.Geom!.Coordinate.Y, t.Geom.Coordinate.X))
            .ToList();
        return new SpatialTowerQueryResponse(dtos.Count, dtos);
    }
}
