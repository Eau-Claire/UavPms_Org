using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Devices.Commands;

public class HeartbeatCommandHandler : IRequestHandler<HeartbeatCommand, object>
{
    private readonly IGenericRepository<Uav> _uavRepository;
    private readonly IUnitOfWork _unitOfWork;

    public HeartbeatCommandHandler(
        IGenericRepository<Uav> uavRepository,
        IUnitOfWork unitOfWork)
    {
        _uavRepository = uavRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<object> Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        var uavs = await _uavRepository.FindAsync(u => u.UavCode == request.DroneId);
        var uav = uavs.FirstOrDefault();

        if (uav == null && Guid.TryParse(request.DroneId, out var uavGuid))
        {
            uav = await _uavRepository.GetByIdAsync(uavGuid);
        }

        if (uav == null)
        {
            return new { error = "Device not registered" };
        }

        uav.BatteryLevel = request.Battery;
        uav.Status = "Online";

        await _uavRepository.UpdateAsync(uav);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new { status = "Ok" };
    }
}
