using MediatR;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.Assets.Commands.DeleteAsset;

public class DeleteAssetCommandHandler : IRequestHandler<DeleteAssetCommand>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssetCommandHandler(IAssetRepository assetRepository, IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("Asset", request.Id);
        }

        await _assetRepository.DeleteAsync(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
