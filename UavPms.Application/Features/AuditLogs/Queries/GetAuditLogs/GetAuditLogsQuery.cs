using MediatR;
using UavPms.Application.Features.AuditLogs.DTOs;

namespace UavPms.Application.Features.AuditLogs.Queries.GetAuditLogs;

public record GetAuditLogsQuery(
    int Page, 
    int PageSize, 
    string? Search = null, 
    string? TableName = null, 
    string? ActionType = null
) : IRequest<PaginatedAuditLogsResponse>;