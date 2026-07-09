using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Missions.DTOs;

public record PaginatedMissionsResponse(
    List<MissionDto> Items,
    PaginationMetaData Pagination);