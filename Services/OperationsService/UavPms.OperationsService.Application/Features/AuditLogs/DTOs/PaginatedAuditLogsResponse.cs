using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.AuditLogs.DTOs;

public record PaginatedAuditLogsResponse(
    List<AuditLogDto> Items,
    PaginationMetaData Pagination);
