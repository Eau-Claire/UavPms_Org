using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Regions.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Regions.Commands.CreateRegion;

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
