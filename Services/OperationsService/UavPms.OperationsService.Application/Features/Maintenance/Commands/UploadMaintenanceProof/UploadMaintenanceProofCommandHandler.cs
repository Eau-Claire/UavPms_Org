using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.UploadMaintenanceProof;

public class UploadMaintenanceProofCommandHandler : IRequestHandler<UploadMaintenanceProofCommand, MaintenanceProofDto>
{
    private readonly IGenericRepository<MaintenanceTicket> _ticketRepository;
    private readonly IGenericRepository<MaintenanceProof> _proofRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UploadMaintenanceProofCommandHandler(
        IGenericRepository<MaintenanceTicket> ticketRepository,
        IGenericRepository<MaintenanceProof> proofRepository,
        IFileStorageService fileStorageService,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _ticketRepository = ticketRepository;
        _proofRepository = proofRepository;
        _fileStorageService = fileStorageService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceProofDto> Handle(UploadMaintenanceProofCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket == null)
            throw new NotFoundException("MaintenanceTicket", request.TicketId);

        var proofType = request.ProofType?.Trim() ?? "After";
        var validTypes = new[] { "Before", "InProgress", "After" };
        if (!validTypes.Contains(proofType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("INVALID_PROOF_TYPE", "ProofType must be one of: 'Before', 'InProgress', 'After'.");
        }

        if (request.File == null || request.File.Length == 0)
        {
            throw new BusinessRuleException("FILE_EMPTY", "Uploaded proof file cannot be empty.");
        }

        string fileUrl;
        await using (var stream = request.File.OpenReadStream())
        {
            fileUrl = await _fileStorageService.SaveImageAsync(stream, request.File.FileName, cancellationToken);
        }

        var proof = new MaintenanceProof
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            UploadedBy = _currentUser.UserId,
            ProofType = proofType,
            FileUrl = fileUrl,
            TechnicianNotes = request.TechnicianNotes ?? string.Empty,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _proofRepository.AddAsync(proof);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MaintenanceProofDto
        {
            Id = proof.Id,
            TicketId = proof.TicketId,
            UploadedBy = proof.UploadedBy,
            ProofType = proof.ProofType,
            FileUrl = proof.FileUrl,
            AfterRepairImageUrl = proof.ProofType.Equals("After", StringComparison.OrdinalIgnoreCase) ? proof.FileUrl : string.Empty,
            TechnicianNotes = proof.TechnicianNotes,
            UploadedAt = proof.UploadedAt
        };
    }
}
