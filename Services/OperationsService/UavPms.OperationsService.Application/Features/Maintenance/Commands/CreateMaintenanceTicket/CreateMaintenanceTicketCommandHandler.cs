using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;

public class CreateMaintenanceTicketCommandHandler : IRequestHandler<CreateMaintenanceTicketCommand, MaintenanceTicketDto>
{
    private readonly IGenericRepository<MaintenanceTicket> _ticketRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMaintenanceTicketCommandHandler(
        IGenericRepository<MaintenanceTicket> ticketRepository,
        ITowerRepository towerRepository,
        IAnomalyRepository anomalyRepository,
        IUserRepository userRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _ticketRepository = ticketRepository;
        _towerRepository = towerRepository;
        _anomalyRepository = anomalyRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceTicketDto> Handle(CreateMaintenanceTicketCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new BusinessRuleException("DESCRIPTION_REQUIRED", "Description is required.");

        TicketPriority priority;
        if (string.Equals(request.Priority, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Priority, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            priority = TicketPriority.Emergency;
        }
        else if (!Enum.TryParse(request.Priority, true, out priority))
        {
            priority = TicketPriority.Medium;
        }

        var tech = await _userRepository.GetByIdAsync(request.TechnicianId);
        if (tech == null)
            throw new NotFoundException("Technician", request.TechnicianId);

        var anomaly = await _anomalyRepository.GetByIdAsync(request.AnomalyId);
        if (anomaly == null)
            throw new NotFoundException("DetectedAnomaly", request.AnomalyId);

        Guid? towerId = request.TowerId;
        if (!towerId.HasValue && !string.IsNullOrWhiteSpace(anomaly.TowerId) && Guid.TryParse(anomaly.TowerId, out var parsedTowerId))
        {
            towerId = parsedTowerId;
        }

        if (towerId.HasValue)
        {
            var tower = await _towerRepository.GetByIdAsync(towerId.Value);
            if (tower != null)
            {
                tower.ActiveDefectsCount++;
                await _towerRepository.UpdateAsync(tower);
            }
        }

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
            AnomalyId = request.AnomalyId,
            AssetId = request.AssetId ?? anomaly.AssetId,
            TowerId = towerId,
            ManagerId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : Guid.NewGuid(),
            TechnicianId = request.TechnicianId,
            Status = TicketStatus.Open,
            Priority = priority,
            Description = request.Description,
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(7),
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _ticketRepository.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MaintenanceTicketDto
        {
            Id = ticket.Id,
            TicketCode = ticket.TicketCode,
            AnomalyId = ticket.AnomalyId,
            AssetId = ticket.AssetId,
            TowerId = ticket.TowerId,
            ManagerId = ticket.ManagerId,
            TechnicianId = ticket.TechnicianId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            Description = ticket.Description,
            DueDate = ticket.DueDate,
            AssignedAt = ticket.AssignedAt,
            CreatedAt = ticket.CreatedAt
        };
    }
}
