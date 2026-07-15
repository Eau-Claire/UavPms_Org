using MediatR;

namespace UavPms.Application.Features.Devices.Commands;

public class HeartbeatCommand : IRequest<object>
{
    public string DroneId { get; set; } = string.Empty;
    public double Battery { get; set; }
    public double Temperature { get; set; }
}
