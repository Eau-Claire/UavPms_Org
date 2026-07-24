using MediatR;
using UavPms.OperationsService.Application.Features.AuditLogs.DTOs;

namespace UavPms.OperationsService.Application.Features.AuditLogs.Queries.GetAuditLogs;

public record GetAuditLogsQuery(
    int Page, 
    int PageSize, 
    string? Search = null, 
    string? TableName = null, 
    string? ActionType = null
) : IRequest<PaginatedAuditLogsResponse>;