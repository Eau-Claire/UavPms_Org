using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.TransmissionLines.Commands.DeleteTransmissionLine;

public class DeleteTransmissionLineCommandHandler : IRequestHandler<DeleteTransmissionLineCommand>
{
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private  readonly IUnitOfWork _unitOfWork;

    public DeleteTransmissionLineCommandHandler(
        ITransmissionLineRepository transmissionLineRepository,
        IUnitOfWork unitOfWork)
    {
        _transmissionLineRepository = transmissionLineRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(DeleteTransmissionLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _transmissionLineRepository.GetByIdAsync(request.Id);
        if (line == null || line.IsDeleted)
        {
            throw new NotFoundException("TransmissionLine", request.Id);
        }
        
        await _transmissionLineRepository.DeleteAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}