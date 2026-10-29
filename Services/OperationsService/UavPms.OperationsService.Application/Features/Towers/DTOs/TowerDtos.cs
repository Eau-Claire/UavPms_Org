using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Towers.DTOs;

public record TowerDto(
    Guid Id,
    Guid RegionId,
    string? RegionName,
    string TowerCode,
    string? PoleType,
    string? Address,
    double Latitude,
    double Longitude)
{
    public static TowerDto From(Tower tower) => new(
        tower.Id,
        tower.RegionId,
        tower.Region?.Name,
        tower.TowerCode,
        tower.PoleType,
        tower.Address,
        tower.Geom?.Coordinate.Y ?? 0.0,
        tower.Geom?.Coordinate.X ?? 0.0);
}

public record PaginatedTowersResponse(
    List<TowerDto> Items,
    PaginationMetaData Pagination);

public record SpatialTowerQueryResponse(
    int Total,
    IReadOnlyList<SpatialTowerDto> Towers);

public record SpatialTowerDto(
    Guid TowerId,
    string TowerCode,
    Guid RegionId,
    double Latitude,
    double Longitude)
{
    public Guid Id => TowerId;
}

/// <summary>
/// Trang chi tiết cột điện: một trang bất thường và một trang lịch sử bảo trì (cùng page/pageSize, phân trang riêng),
/// kèm thống kê đếm trực tiếp trên toàn bộ cột (không theo bộ lọc, không lưu cột tổng hợp).
/// </summary>
public record TowerHistoryDto(
    TowerDto Tower,
    IReadOnlyList<TowerAnomalyDto> Anomalies,
    IReadOnlyList<TowerMaintenanceDto> MaintenanceHistory,
    TowerHistoryStatsDto Stats,
    PaginationMetaData AnomaliesPagination,
    PaginationMetaData MaintenancePagination);

/// <summary>
/// Open anomalies = Pending + Confirmed (not yet rejected or resolved); superseded edge results are excluded.
/// Active tickets = Open + InProgress + PendingVerification.
/// </summary>
public record TowerHistoryStatsDto(
    int TotalAnomalies,
    int PendingAnomalies,
    int ConfirmedAnomalies,
    int OpenAnomalies,
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int ActiveTickets,
    DateTime? LastInspectedAt);

public record TowerAnomalyDto(
    Guid Id,
    Guid MediaId,
    Guid MissionId,
    string? CategoryCode,
    string? CategoryName,
    double ConfidenceScore,
    string ValidationStatus,
    string AiSource,
    string? ImageUrl,
    string? CropUrl,
    DateTime DetectedAt,
    DateTime? ValidatedAt);

public record TowerMaintenanceDto(
    Guid Id,
    string TicketCode,
    Guid AnomalyId,
    string Status,
    string Priority,
    string Description,
    Guid ManagerId,
    Guid? TechnicianId,
    DateTime CreatedAt,
    DateTime? DueDate,
    DateTime? StartedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<TowerMaintenanceProofDto> Proofs,
    IReadOnlyList<TowerMaterialLogDto> Materials);

public record TowerMaintenanceProofDto(Guid Id, string ProofType, string FileUrl, string TechnicianNotes, DateTime UploadedAt);

public record TowerMaterialLogDto(Guid Id, string ComponentCode, string ComponentName, int QuantityUsed, string Unit, DateTime LoggedAt);
