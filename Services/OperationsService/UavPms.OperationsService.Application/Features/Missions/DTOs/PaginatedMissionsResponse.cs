using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.DTOs;

public record PaginatedMissionsResponse(
    List<MissionDto> Items,
    PaginationMetaData Pagination);