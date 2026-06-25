using UavPms.Application.Features.Users.DTOs;
using UavPms.Core.Entities;

namespace UavPms.Application.Features.AuditLogs.DTOs;

public record PaginatedAuditLogsResponse(
    List<AuditLogDto> Items,
    PaginationMetaData Pagination);