using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Devices.Commands;

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, object>
{
    private readonly IGenericRepository<Uav> _uavRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceCommandHandler(
        IGenericRepository<Uav> uavRepository,
        IUnitOfWork unitOfWork)
    {
        _uavRepository = uavRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<object> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var uavs = await _uavRepository.FindAsync(u => u.UavCode == request.SerialNumber);
        var uav = uavs.FirstOrDefault();

        if (uav != null)
        {
            if (uav.Status == "Pending")
            {
                return new { status = "Pending" };
            }
            return new
            {
                droneId = uav.UavCode,
                deviceToken = uav.Id.ToString()
            };
        }

        var newUav = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = request.SerialNumber,
            Model = $"Raspberry Pi (SW: {request.SoftwareVersion})",
            Status = "Pending",
            BatteryLevel = 100
        };

        await _uavRepository.AddAsync(newUav);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new { status = "Pending" };
    }
}
