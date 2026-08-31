using MediatR;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.DeleteAssetComponent;

public class DeleteAssetComponentCommandHandler : IRequestHandler<DeleteAssetComponentCommand>
{
    private readonly IAssetComponentRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssetComponentCommandHandler(IAssetComponentRepository assetRepository, IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteAssetComponentCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("AssetComponent", request.Id);
        }

        await _assetRepository.DeleteAsync(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
