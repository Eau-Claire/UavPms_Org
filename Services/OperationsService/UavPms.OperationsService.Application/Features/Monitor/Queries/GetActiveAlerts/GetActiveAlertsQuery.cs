using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetActiveAlerts;

public record GetActiveAlertsQuery : IRequest<ActiveAlertsResponse>;

public record ActiveAlertDto(
    Guid NotificationId,
    string Title,
    string Message,
    DateTime CreatedAt,
    bool IsRead
);

public record ActiveAlertsResponse(
    List<ActiveAlertDto> Items
);