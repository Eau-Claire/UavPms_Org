using MediatR;
using UavPms.Application.Features.AuditLogs.DTOs;
using UavPms.Application.Common.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.AuditLogs.Queries.GetAuditLogs;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedAuditLogsResponse>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<PaginatedAuditLogsResponse> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var (logs, totalCount) = await _auditLogRepository.GetAuditLogsPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.TableName,
            request.ActionType);
        
        var dtos = logs.Select(x => new AuditLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            OperatorUsername = x.User?.Username,
            TableName =  x.TableName,
            RecordId =  x.RecordId,
            ActionType =  x.ActionType,
            OldValues =  x.OldValues,
            NewValues =   x.NewValues,
            IpAddress =  x.IpAddress,
            UserAgent =   x.UserAgent,
            CreatedAt =   x.CreatedAt,
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var metaData = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedAuditLogsResponse(dtos, metaData);
    }
}