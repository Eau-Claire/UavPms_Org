using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Regions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Regions.Commands.CreateRegion;

public class CreateRegionCommandHandler : IRequestHandler<CreateRegionCommand, RegionDto>
{
    private readonly IRegionRepository _regionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRegionCommandHandler(IRegionRepository regionRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegionDto> Handle(CreateRegionCommand request, CancellationToken cancellationToken)
    {
        var region = new Region
        {
            Id = Guid.NewGuid(),
            RegionName = request.RegionName,
            CreatedAt = DateTime.UtcNow
        };

        await _regionRepository.AddAsync(region);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegionDto(region.Id, region.RegionName, null);
    }
}
