using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Towers.Commands.DeleteTower;

public class DeleteTowerCommandHandler : IRequestHandler<DeleteTowerCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITowerRepository _towerRepository;

    public DeleteTowerCommandHandler(
        ITowerRepository towerRepository,
        IUnitOfWork unitOfWork)
    {
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(DeleteTowerCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.Id);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.Id);
        }
        
        tower.IsDeleted = true;
        tower.UpdatedAt = DateTime.UtcNow;
        
        await _towerRepository.UpdateAsync(tower);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}