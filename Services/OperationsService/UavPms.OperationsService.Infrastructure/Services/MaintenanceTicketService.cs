using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Application.Features.Maintenance;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Services;

public class MaintenanceTicketService : IMaintenanceTicketService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _currentUser;
    private readonly IMissionRealtimeNotifier? _notifier;
    private readonly IFileStorageService? _fileStorage;

    public MaintenanceTicketService(
        ApplicationDbContext db,
        ICurrentUserServices currentUser,
        IMissionRealtimeNotifier? notifier = null,
        IFileStorageService? fileStorage = null)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
        _fileStorage = fileStorage;
    }

    public async Task<MaintenanceTicketDto> CreateTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default)
    {
        // 1. Description validation
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 1000)
        {
            throw new BusinessRuleException("Description is required and max 1000 characters");
        }

        // 2. Priority validation
        TicketPriority parsedPriority;
        if (string.Equals(request.Priority, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            parsedPriority = TicketPriority.Emergency;
        }
        else if (!Enum.TryParse(request.Priority, true, out parsedPriority))
        {
            throw new BusinessRuleException("Priority must be one of: Low, Medium, High, Emergency");
        }

        // 3. DueDate validation
        if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new BusinessRuleException("Due date cannot be in the past");
        }

        // 4. Technician validation
        var tech = await LoadTechnicianAsync(request.TechnicianId, ct);

        // 5. Cột điện của phiếu suy ra từ bất thường (anomaly.TowerId), không nhận từ request.

        // 6. Anomaly validation
        var anomaly = await _db.DetectedAnomalies
            .Include(a => a.Tower)
            .FirstOrDefaultAsync(a => a.Id == request.AnomalyId && !a.IsDeleted, ct);
        if (anomaly == null)
        {
            throw new NotFoundException("Anomaly not found");
        }

        // Manager chỉ tạo phiếu cho defect trong vùng của mình (SystemAdmin mọi vùng).
        if (!await RegionScope.IsGrantedAsync(_db, _currentUser, await AnomalyRegionIdAsync(anomaly.Id, ct), ct))
        {
            throw new ForbiddenException("TICKET_REGION_SCOPE_REQUIRED");
        }

        // 7. Generate TicketCode
        var today = DateTime.UtcNow.Date;
        var countToday = await _db.MaintenanceTickets.CountAsync(t => t.CreatedAt >= today, ct);
        var ticketCode = $"TCK-{DateTime.UtcNow:yyyyMMdd}-{(countToday + 1):D3}";

        var managerId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : (tech.CreatedBy ?? Guid.Empty);
        var now = DateTime.UtcNow;

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            AnomalyId = request.AnomalyId,
            Anomaly = anomaly,
            ManagerId = managerId,
            TechnicianId = request.TechnicianId,
            Technician = tech,
            Status = request.TechnicianId != Guid.Empty ? TicketStatus.Assigned : TicketStatus.Open,
            Priority = parsedPriority,
            Description = request.Description,
            DueDate = request.DueDate,
            AssignedAt = now,
            CreatedAt = now,
            IsDeleted = false
        };

        _db.MaintenanceTickets.Add(ticket);

        // Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = managerId,
            TableName = "MaintenanceTickets",
            RecordId = ticket.Id,
            ActionType = "TicketCreated",
            NewValues = JsonSerializer.Serialize(new { ticket.TicketCode, ticket.Status, ticket.Priority, ticket.TechnicianId }),
            OldValues = "{}",
            IpAddress = _currentUser.IpAddress ?? "",
            UserAgent = _currentUser.UserAgent ?? "",
            CreatedAt = now
        });

        _db.Notifications.Add(new Notification
        {
            UserId = tech.Id,
            Title = "Phiếu bảo trì mới",
            Body = $"Bạn được giao phiếu bảo trì {ticket.TicketCode}.",
            Type = "MAINTENANCE_ASSIGNED",
            ReferenceType = "MaintenanceTicket",
            ReferenceId = ticket.Id,
        });

        QueueMaintenanceTicketAssignedEvent(ticket, tech.Id, anomaly.TowerId, now);

        await _db.SaveChangesAsync(ct);

        return MapToDto(ticket);
    }

    /// <summary>
    /// Giao (hoặc đổi) kỹ thuật viên cho phiếu chưa bắt đầu; phiếu tự sinh khi duyệt bất thường chưa có kỹ thuật viên.
    /// </summary>
    public async Task<MaintenanceTicketDto> AssignTechnicianAsync(Guid ticketId, AssignTechnicianRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets
            .Include(t => t.Anomaly)
                .ThenInclude(a => a!.Tower)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct)
            ?? throw new NotFoundException("Maintenance ticket not found");

        await EnsureManagerTicketScopeAsync(ticket, ct);

        if (ticket.Status is not (TicketStatus.Open or TicketStatus.Assigned))
        {
            throw new BusinessRuleException("TICKET_NOT_OPEN", $"Only Open or Assigned tickets can be (re)assigned; this ticket is '{ticket.Status}'.");
        }

        if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new BusinessRuleException("Due date cannot be in the past");
        }

        var tech = await LoadTechnicianAsync(request.TechnicianId, ct);
        var old = new { ticket.TechnicianId, ticket.DueDate, Status = ticket.Status.ToString() };
        var now = DateTime.UtcNow;

        ticket.TechnicianId = tech.Id;
        ticket.Technician = tech;
        ticket.Status = TicketStatus.Assigned;
        ticket.DueDate = request.DueDate ?? ticket.DueDate;
        ticket.AssignedAt = now;
        ticket.UpdatedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            TableName = "MaintenanceTickets",
            RecordId = ticket.Id,
            ActionType = "TicketAssigned",
            OldValues = JsonSerializer.Serialize(old),
            NewValues = JsonSerializer.Serialize(new { ticket.TechnicianId, ticket.DueDate }),
            IpAddress = _currentUser.IpAddress ?? "",
            UserAgent = _currentUser.UserAgent ?? "",
            CreatedAt = now
        });

        _db.Notifications.Add(new Notification
        {
            UserId = tech.Id,
            Title = "Phiếu bảo trì mới",
            Body = $"Bạn được giao phiếu bảo trì {ticket.TicketCode}.",
            Type = "MAINTENANCE_ASSIGNED",
            ReferenceType = "MaintenanceTicket",
            ReferenceId = ticket.Id,
        });

        QueueMaintenanceTicketAssignedEvent(ticket, tech.Id, ticket.Anomaly?.TowerId, now);

        await _db.SaveChangesAsync(ct);
        return MapToDto(ticket);
    }

    private void QueueMaintenanceTicketAssignedEvent(MaintenanceTicket ticket, Guid technicianId, Guid? towerId, DateTime assignedAt)
    {
        var ticketAssignedEvent = new MaintenanceTicketAssignedEvent
        {
            EventId = Guid.NewGuid(),
            TicketId = ticket.Id,
            TicketCode = ticket.TicketCode,
            Title = $"Phiếu bảo trì {ticket.TicketCode}",
            TowerId = towerId ?? Guid.Empty,
            AssignedToUserId = technicianId,
            Priority = ticket.Priority.ToString(),
            AnomalyDescription = ticket.Description,
            AssignedAt = assignedAt
        };

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = ticketAssignedEvent.EventId,
            MessageType = nameof(MaintenanceTicketAssignedEvent),
            Payload = JsonSerializer.Serialize(ticketAssignedEvent),
            OccurredAt = assignedAt
        });
    }

    /// <summary>Kỹ thuật viên đang hoạt động kèm số phiếu chưa đóng, để chọn khi giao phiếu.</summary>
    public async Task<IReadOnlyList<TechnicianSummaryDto>> GetTechniciansAsync(CancellationToken ct = default)
    {
        var technicianRoles = new[] { UserRoles.Technician.ToLower(), UserRoles.Technician.ToLower() };
        return await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && (u.Status == "Active" || u.Status == "Enabled") &&
                        u.UserRoles.Any(ur => technicianRoles.Contains(ur.Role!.RoleName.ToLower())))
            .OrderBy(u => u.FullName)
            .Select(u => new TechnicianSummaryDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                ActiveTicketCount = _db.MaintenanceTickets.Count(t => t.TechnicianId == u.Id && !t.IsDeleted &&
                    t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled)
            })
            .ToListAsync(ct);
    }

    private async Task<User> LoadTechnicianAsync(Guid technicianId, CancellationToken ct)
    {
        var tech = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == technicianId && !u.IsDeleted, ct)
            ?? throw new NotFoundException("Technician not found");

        var isTechnician = tech.UserRoles.Any(ur =>
            string.Equals(ur.Role?.RoleName, UserRoles.Technician, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ur.Role?.RoleName, UserRoles.Technician, StringComparison.OrdinalIgnoreCase));

        if (!isTechnician)
        {
            throw new BusinessRuleException("Assigned user is not a Technician");
        }

        return tech;
    }

    public async Task<MaintenanceTicketDto> UpdateTicketStatusAsync(Guid ticketId, string newStatus, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);

        if (ticket == null)
        {
            throw new NotFoundException("Maintenance ticket not found");
        }

        // IDOR Guard
        await EnsureTicketAccessAsync(ticket, ct);

        // Terminal State Guard (Closed / Cancelled)
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new BusinessRuleException($"Cannot modify status of a terminal ticket ('{ticket.Status}').");
        }

        if (string.Equals(newStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            if (ticket.Status == TicketStatus.PendingVerification)
            {
                // MF04 Activity Diagram: Manager rejects repair proofs -> rework required -> InProgress
                if (!IsManagerOrAdmin())
                {
                    throw new ForbiddenException("TICKET_REJECT_REQUIRES_MANAGER: Only a Manager or SystemAdmin can reject repair proofs for rework.");
                }
                ticket.Status = TicketStatus.InProgress;
                if (ticket.TechnicianId.HasValue)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = ticket.TechnicianId.Value,
                        Title = "Yêu cầu sửa chữa lại",
                        Body = $"Minh chứng cho phiếu bảo trì {ticket.TicketCode} bị từ chối. Vui lòng thực hiện và tải lại minh chứng.",
                        Type = "MAINTENANCE_REWORK_REQUIRED",
                        ReferenceType = "MaintenanceTicket",
                        ReferenceId = ticket.Id,
                    });
                }
            }
            else if (ticket.Status is TicketStatus.Assigned or TicketStatus.Open)
            {
                if (ticket.TechnicianId == null)
                {
                    throw new BusinessRuleException("Cannot start progress on a ticket that has no technician assigned.");
                }
                ticket.Status = TicketStatus.InProgress;
                ticket.StartedAt = DateTime.UtcNow;
            }
            else
            {
                throw new BusinessRuleException($"Cannot transition ticket from '{ticket.Status}' to InProgress.");
            }
        }
        else if (string.Equals(newStatus, "PendingVerification", StringComparison.OrdinalIgnoreCase))
        {
            if (ticket.Status != TicketStatus.InProgress)
            {
                throw new BusinessRuleException($"Cannot transition ticket directly from {ticket.Status} to PendingVerification");
            }
            var hasProof = await _db.MaintenanceProofs.AnyAsync(p => p.TicketId == ticketId && !p.IsDeleted, ct);
            if (!hasProof)
            {
                throw new BusinessRuleException("Cannot submit ticket for verification without repair proof photo");
            }
            ticket.Status = TicketStatus.PendingVerification;
        }
        else if (string.Equals(newStatus, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            // Closing is the manager's acceptance step: same rules, audit and anomaly update as PUT /close.
            if (!IsManagerOrAdmin())
            {
                throw new ForbiddenException("TICKET_CLOSE_REQUIRES_MANAGER: Only a Manager or SystemAdmin can accept and close a ticket.");
            }
            return await CloseTicketAsync(ticketId, ct);
        }
        else if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsManagerOrAdmin())
            {
                throw new ForbiddenException("TICKET_CANCEL_REQUIRES_MANAGER: Only a Manager or SystemAdmin can cancel a ticket.");
            }
            if (ticket.Status is not (TicketStatus.Open or TicketStatus.Assigned))
            {
                throw new BusinessRuleException($"Only Open or Assigned tickets can be cancelled; current status is '{ticket.Status}'.");
            }
            ticket.Status = TicketStatus.Cancelled;
        }
        else
        {
            throw new BusinessRuleException($"Invalid status: {newStatus}");
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (_notifier != null && ticket.AnomalyId != Guid.Empty)
        {
            try
            {
                var missionId = await _db.DetectedAnomalies
                    .Where(a => a.Id == ticket.AnomalyId)
                    .Select(a => a.Media != null ? a.Media.MissionId : (Guid?)null)
                    .FirstOrDefaultAsync(ct);

                if (missionId.HasValue)
                {
                    await _notifier.NotifyAsync(new MissionLifecycleEventDto
                    {
                        MissionId = missionId.Value.ToString(),
                        Type = "MAINTENANCE_TASK_STATUS_CHANGED",
                        Status = ticket.Status.ToString(),
                        Reason = $"Ticket {ticket.TicketCode} status updated to {ticket.Status}",
                        Timestamp = DateTime.UtcNow
                    }, ct);
                }
            }
            catch
            {
                // Non-blocking realtime event
            }
        }

        return MapToDto(ticket);
    }

    public async Task<MaintenanceProofDto> UploadProofAsync(Guid ticketId, IFormFile file, string? technicianNotes, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets.FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);
        if (ticket == null)
        {
            throw new NotFoundException("Maintenance ticket not found");
        }

        EnsureAssignedTechnicianOrAdmin(ticket);
        EnsureTicketEditable(ticket);

        if (file == null || file.Length == 0 || file.Length > 50 * 1024 * 1024)
        {
            throw new BusinessRuleException("File must not be empty and cannot exceed 50MB");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var prohibited = new[] { ".exe", ".php", ".sh", ".bat", ".cmd", ".dll", ".com", ".py", ".js" };
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff" };

        if (prohibited.Contains(ext) || !allowed.Contains(ext))
        {
            throw new BusinessRuleException("File extension is prohibited");
        }

        // The proof photo is stored in MinIO (or local storage without MinIO); a placeholder URL is used only
        // when no storage service is wired (unit tests).
        string fileUrl;
        if (_fileStorage != null)
        {
            await using var stream = file.OpenReadStream();
            try
            {
                fileUrl = await _fileStorage.SaveImageAsync(stream, file.FileName, ct);
            }
            catch (Exception ex) when (ex is not BusinessRuleException)
            {
                throw new InfrastructureOperationException("STORAGE_FAILURE", "Failed to store maintenance proof.", ex);
            }
        }
        else
        {
            fileUrl = $"https://storage.uavpms.internal/proofs/{ticket.Id}/{FileSanitizer.GenerateUniqueFileName(file.FileName)}";
        }

        var proof = new MaintenanceProof
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            UploadedBy = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : ticket.TechnicianId ?? ticket.ManagerId,
            ProofType = "After",
            FileUrl = fileUrl,
            TechnicianNotes = technicianNotes ?? "",
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.MaintenanceProofs.Add(proof);
        await _db.SaveChangesAsync(ct);

        return new MaintenanceProofDto
        {
            Id = proof.Id,
            TicketId = proof.TicketId,
            UploadedBy = proof.UploadedBy,
            FileUrl = proof.FileUrl,
            AfterRepairImageUrl = proof.ProofType == "After" ? proof.FileUrl : string.Empty,
            TechnicianNotes = proof.TechnicianNotes,
            UploadedAt = proof.UploadedAt
        };
    }

    public async Task<MaterialLogDto> LogMaterialAsync(Guid ticketId, LogMaterialRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets.FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);
        if (ticket == null)
        {
            throw new NotFoundException("Maintenance ticket not found");
        }

        EnsureAssignedTechnicianOrAdmin(ticket);
        EnsureTicketEditable(ticket);

        if (string.IsNullOrWhiteSpace(request.ComponentName) || string.IsNullOrWhiteSpace(request.Unit))
        {
            throw new BusinessRuleException("componentName and unit are required");
        }

        if (request.QuantityUsed <= 0)
        {
            throw new BusinessRuleException("quantityUsed must be greater than 0");
        }

        var material = new MaterialLog
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LoggedBy = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : ticket.TechnicianId ?? ticket.ManagerId,
            ComponentName = request.ComponentName,
            ComponentCode = request.ComponentCode ?? "",
            QuantityUsed = request.QuantityUsed,
            Unit = request.Unit,
            FieldObservations = request.FieldObservations ?? "",
            LoggedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.MaterialLogs.Add(material);
        await _db.SaveChangesAsync(ct);

        return new MaterialLogDto
        {
            Id = material.Id,
            TicketId = material.TicketId,
            LoggedBy = material.LoggedBy,
            ComponentName = material.ComponentName,
            ComponentCode = material.ComponentCode,
            QuantityUsed = material.QuantityUsed,
            Unit = material.Unit,
            FieldObservations = material.FieldObservations,
            LoggedAt = material.LoggedAt
        };
    }

    public async Task<MaintenanceTicketDto> CloseTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);

        if (ticket == null)
        {
            throw new NotFoundException("Maintenance ticket not found");
        }

        await EnsureManagerTicketScopeAsync(ticket, ct);

        if (ticket.Status != TicketStatus.PendingVerification)
        {
            throw new BusinessRuleException("TICKET_NOT_PENDING_VERIFICATION", $"Cannot close ticket from status '{ticket.Status}'. Must be in PendingVerification.");
        }

        var hasProof = await _db.MaintenanceProofs.AnyAsync(p => p.TicketId == ticketId && !p.IsDeleted, ct);
        if (!hasProof)
        {
            throw new BusinessRuleException("Cannot close ticket without repair proof photo");
        }

        var oldStatus = ticket.Status.ToString();
        ticket.Status = TicketStatus.Closed;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Auto-resolve linked anomaly
        var anomaly = await _db.DetectedAnomalies.FirstOrDefaultAsync(a => a.Id == ticket.AnomalyId, ct);
        if (anomaly != null)
        {
            anomaly.ValidationStatus = "Resolved";
            anomaly.UpdatedAt = DateTime.UtcNow;
        }

        // Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : ticket.ManagerId,
            TableName = "MaintenanceTickets",
            RecordId = ticket.Id,
            ActionType = "TicketClosed",
            NewValues = JsonSerializer.Serialize(new { ticket.TicketCode, Status = "Resolved", ResolvedAt = ticket.ResolvedAt }),
            OldValues = JsonSerializer.Serialize(new { ticket.TicketCode, Status = oldStatus }),
            IpAddress = _currentUser.IpAddress ?? "",
            UserAgent = _currentUser.UserAgent ?? "",
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (anomaly != null && ex.InnerException?.Message.Contains("CK_DetectedAnomalies_ValidationStatus") == true)
        {
            _db.Entry(anomaly).State = EntityState.Unchanged;
            await _db.SaveChangesAsync(ct);
        }

        if (_notifier != null && ticket.AnomalyId != Guid.Empty)
        {
            try
            {
                var missionId = await _db.DetectedAnomalies
                    .Where(a => a.Id == ticket.AnomalyId)
                    .Select(a => a.Media != null ? a.Media.MissionId : (Guid?)null)
                    .FirstOrDefaultAsync(ct);

                if (missionId.HasValue)
                {
                    await _notifier.NotifyAsync(new MissionLifecycleEventDto
                    {
                        MissionId = missionId.Value.ToString(),
                        Type = "MAINTENANCE_TASK_STATUS_CHANGED",
                        Status = "Closed",
                        Reason = $"Ticket {ticket.TicketCode} closed and resolved",
                        Timestamp = DateTime.UtcNow
                    }, ct);
                }
            }
            catch
            {
                // Non-blocking realtime event
            }
        }

        return MapToDto(ticket);
    }

    public async Task<IReadOnlyList<MaintenanceTicketDto>> GetMyTicketsAsync(CancellationToken ct = default)
    {
        var tickets = await _db.MaintenanceTickets
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Include(t => t.Technician)
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .AsSplitQuery()
            .Where(t => t.TechnicianId == _currentUser.UserId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(MapToDto).ToList();
    }

    public async Task<PagedTicketsDto> GetTicketsAsync(int pageIndex, int pageSize, string? status, string? priority, CancellationToken ct = default)
    {
        var query = _db.MaintenanceTickets
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Include(t => t.Technician)
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .AsSplitQuery()
            .Where(t => !t.IsDeleted);

        // Spec §4: SystemAdmin thấy mọi phiếu; Technician thấy phiếu được giao; Manager thấy phiếu mình phụ trách
        // hoặc phiếu trong vùng (vùng theo cột điện của defect, defect chưa gắn cột thì theo vùng nhiệm vụ).
        if (!RegionScope.IsSystemAdmin(_currentUser))
        {
            var uid = _currentUser.UserId;
            var isManager = RegionScope.HasRole(_currentUser, UserRoles.Manager);
            var regionIds = RegionScope.RegionIds(_db, uid);
            query = query.Where(t => t.TechnicianId == uid || (isManager && (
                t.ManagerId == uid ||
                (t.Anomaly!.TowerId != null && regionIds.Contains(t.Anomaly.Tower!.RegionId)) ||
                (t.Anomaly!.TowerId == null && t.Anomaly.Media!.Mission!.RegionId != null
                    && regionIds.Contains(t.Anomaly.Media.Mission.RegionId.Value)))));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<TicketStatus>(status, true, out var s))
            {
                query = query.Where(t => t.Status == s);
            }
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (Enum.TryParse<TicketPriority>(priority, true, out var p))
            {
                query = query.Where(t => t.Priority == p);
            }
        }

        var total = await query.CountAsync(ct);
        var skip = Math.Max(0, pageIndex) * Math.Max(1, pageSize);
        var items = await query.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(pageSize).ToListAsync(ct);

        return new PagedTicketsDto
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<MaintenanceTicketDto> GetTicketByIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.MaintenanceTickets
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Media)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);

        if (ticket == null)
        {
            throw new NotFoundException("Maintenance ticket not found");
        }

        await EnsureTicketAccessAsync(ticket, ct);

        var dto = MapToDto(ticket);
        if (ticket.Anomaly?.Tower != null)
        {
            dto.Tower = new { ticket.Anomaly.Tower.Id, ticket.Anomaly.Tower.TowerCode, ticket.Anomaly.Tower.PoleType, ticket.Anomaly.Tower.RegionId };
        }
        if (ticket.Anomaly != null)
        {
            var a = ticket.Anomaly;
            dto.Anomaly = new
            {
                a.Id,
                a.ValidationStatus,
                a.ConfidenceScore,
                a.MediaId,
                a.Media?.MissionId,
                CategoryCode = a.Category?.CategoryCode,
                CategoryName = a.Category?.CategoryName,
                a.Category?.IsEmergencyClass,
                ImageUrl = a.ImageUrl ?? a.Media?.FileUrl,
                a.CropUrl,
                a.BoundingBox,
                a.AnalystNotes,
                a.ValidatedAt
            };
        }
        if (ticket.Manager != null)
        {
            dto.Manager = new { ticket.Manager.Id, ticket.Manager.Email, FullName = ticket.Manager.FullName, Username = ticket.Manager.Email };
        }
        if (ticket.Technician != null)
        {
            dto.Technician = new { ticket.Technician.Id, ticket.Technician.Email, FullName = ticket.Technician.FullName, Username = ticket.Technician.Email };
        }

        return dto;
    }

    private bool IsManagerOrAdmin() =>
        (_currentUser.Roles ?? Array.Empty<string>()).Any(r =>
            string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));

    /// <summary>Proofs and materials belong to the repair, so they are only accepted while the ticket is open for work.</summary>
    private static void EnsureTicketEditable(MaintenanceTicket ticket)
    {
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new BusinessRuleException("TICKET_CLOSED", "The ticket is already closed.");
        }
    }

    /// <summary>Minh chứng và vật tư: chỉ Technician được giao phiếu (và SystemAdmin) ghi (spec §4).</summary>
    private void EnsureAssignedTechnicianOrAdmin(MaintenanceTicket ticket)
    {
        if (RegionScope.IsSystemAdmin(_currentUser) || ticket.TechnicianId == _currentUser.UserId)
            return;
        throw new ForbiddenException("Technician is not assigned to this ticket");
    }

    /// <summary>Vùng của phiếu: theo cột điện của defect; defect chưa gắn cột thì theo vùng của nhiệm vụ.</summary>
    private Task<Guid?> AnomalyRegionIdAsync(Guid anomalyId, CancellationToken ct) =>
        _db.DetectedAnomalies
            .Where(a => a.Id == anomalyId)
            .Select(a => a.Tower != null ? (Guid?)a.Tower.RegionId : a.Media!.Mission!.RegionId)
            .FirstOrDefaultAsync(ct);

    /// <summary>Manager chỉ thao tác phiếu do mình phụ trách hoặc phiếu trong vùng của mình (spec §4).</summary>
    private async Task<bool> IsManagerOfTicketAsync(MaintenanceTicket ticket, CancellationToken ct)
    {
        if (!RegionScope.HasRole(_currentUser, UserRoles.Manager))
            return false;
        if (ticket.ManagerId == _currentUser.UserId)
            return true;
        return await RegionScope.IsGrantedAsync(_db, _currentUser, await AnomalyRegionIdAsync(ticket.AnomalyId, ct), ct);
    }

    /// <summary>Giao / đóng phiếu: SystemAdmin hoặc Manager của vùng.</summary>
    private async Task EnsureManagerTicketScopeAsync(MaintenanceTicket ticket, CancellationToken ct)
    {
        if (RegionScope.IsSystemAdmin(_currentUser) || await IsManagerOfTicketAsync(ticket, ct))
            return;
        throw new ForbiddenException("TICKET_REGION_SCOPE_REQUIRED");
    }

    /// <summary>
    /// Xem / xử lý phiếu: SystemAdmin, Technician được giao phiếu, hoặc Manager của vùng.
    /// Quyền theo trạng thái (trả lại, đóng, hủy) vẫn kiểm tra riêng trong UpdateTicketStatusAsync.
    /// </summary>
    private async Task EnsureTicketAccessAsync(MaintenanceTicket ticket, CancellationToken ct)
    {
        if (RegionScope.IsSystemAdmin(_currentUser) || ticket.TechnicianId == _currentUser.UserId)
            return;
        if (await IsManagerOfTicketAsync(ticket, ct))
            return;
        throw new ForbiddenException("Technician is not assigned to this ticket");
    }

    private static MaintenanceTicketDto MapToDto(MaintenanceTicket t)
    {
        return new MaintenanceTicketDto
        {
            Id = t.Id,
            TicketCode = t.TicketCode,
            AnomalyId = t.AnomalyId,
            TowerId = t.Anomaly?.TowerId,
            ManagerId = t.ManagerId,
            TechnicianId = t.TechnicianId,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            Description = t.Description,
            DueDate = t.DueDate,
            AssignedAt = t.AssignedAt,
            StartedAt = t.StartedAt,
            ResolvedAt = t.ResolvedAt,
            CreatedAt = t.CreatedAt,
            // Set when the query loaded them (lists and detail), so list rows can show tower and technician.
            Tower = t.Anomaly?.Tower == null ? null : new { t.Anomaly.Tower.Id, t.Anomaly.Tower.TowerCode, t.Anomaly.Tower.PoleType, t.Anomaly.Tower.RegionId },
            Technician = t.Technician == null ? null : new { t.Technician.Id, t.Technician.Email, t.Technician.FullName, Username = t.Technician.Email },
            MaintenanceProofs = t.MaintenanceProofs?.Where(p => !p.IsDeleted).Select(p => new MaintenanceProofDto
            {
                Id = p.Id,
                TicketId = p.TicketId,
                UploadedBy = p.UploadedBy,
                ProofType = p.ProofType,
                FileUrl = p.FileUrl,
                AfterRepairImageUrl = p.ProofType == "After" ? p.FileUrl : string.Empty,
                TechnicianNotes = p.TechnicianNotes,
                UploadedAt = p.UploadedAt
            }).ToList() ?? new(),
            MaterialLogs = t.MaterialLogs?.Where(m => !m.IsDeleted).Select(m => new MaterialLogDto
            {
                Id = m.Id,
                TicketId = m.TicketId,
                LoggedBy = m.LoggedBy,
                ComponentName = m.ComponentName,
                ComponentCode = m.ComponentCode,
                QuantityUsed = m.QuantityUsed,
                Unit = m.Unit,
                FieldObservations = m.FieldObservations,
                LoggedAt = m.LoggedAt
            }).ToList() ?? new()
        };
    }
}
