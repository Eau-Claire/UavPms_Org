using MediatR;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Common.Exceptions;

namespace UavPms.Application.Features.Regions.Commands.DeleteRegion;

public class DeleteRegionCommandHandler : IRequestHandler<DeleteRegionCommand>
{
    private readonly IRegionRepository _regionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRegionCommandHandler(IRegionRepository regionRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteRegionCommand request, CancellationToken cancellationToken)
    {
        var region = await _regionRepository.GetByIdAsync(request.Id);
        if (region == null || region.IsDeleted)
        {
            throw new NotFoundException("Region", request.Id);
        }

        await _regionRepository.DeleteAsync(region);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
