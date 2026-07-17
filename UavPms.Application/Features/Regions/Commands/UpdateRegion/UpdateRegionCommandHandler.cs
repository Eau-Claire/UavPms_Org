using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Regions.DTOs;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Common.Exceptions;

namespace UavPms.Application.Features.Regions.Commands.UpdateRegion;

public class UpdateRegionCommandHandler : IRequestHandler<UpdateRegionCommand, RegionDto>
{
    private readonly IRegionRepository _regionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRegionCommandHandler(IRegionRepository regionRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegionDto> Handle(UpdateRegionCommand request, CancellationToken cancellationToken)
    {
        var region = await _regionRepository.GetByIdAsync(request.Id);
        if (region == null || region.IsDeleted)
        {
            throw new NotFoundException("Region", request.Id);
        }

        region.RegionName = request.RegionName;
        region.UpdatedAt = DateTime.UtcNow;

        await _regionRepository.UpdateAsync(region);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegionDto(region.Id, region.RegionName, region.Geom?.AsText());
    }
}
