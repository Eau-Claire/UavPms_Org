using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Devices.Commands;

public class RegisterDeviceCommand : IRequest<object>
{
    public string SerialNumber { get; set; } = string.Empty;
    public string SoftwareVersion { get; set; } = string.Empty;
}
