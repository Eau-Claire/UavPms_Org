using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Substations.Commands.DeleteSubstation;

public class DeleteSubstationCommandHandler : IRequestHandler<DeleteSubstationCommand>
{
    private readonly ISubstationRepository _substationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSubstationCommandHandler(
        ISubstationRepository substationRepository,
        IUnitOfWork unitOfWork)
    {
        _substationRepository = substationRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(DeleteSubstationCommand request, CancellationToken cancellationToken)
    {
        var substaion = await _substationRepository.GetByIdAsync(request.Id);
        if (substaion == null || substaion.IsDeleted)
        {
            throw new NotFoundException("Substation", request.Id);
        }
        
        substaion.IsDeleted = true;
        substaion.DeletedAt = DateTime.UtcNow;
        
        await _substationRepository.UpdateAsync(substaion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}