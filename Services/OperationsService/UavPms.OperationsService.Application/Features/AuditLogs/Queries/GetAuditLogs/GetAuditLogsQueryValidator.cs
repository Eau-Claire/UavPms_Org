using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
