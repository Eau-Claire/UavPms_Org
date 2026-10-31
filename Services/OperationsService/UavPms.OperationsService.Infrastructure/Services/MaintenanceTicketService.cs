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
        var anomalyExists = await _db.DetectedAnomalies.AnyAsync(a => a.Id == request.AnomalyId && !a.IsDeleted, ct);
        if (!anomalyExists)
        {
            throw new NotFoundException("Anomaly not found");
        }

        // 7. Generate TicketCode
        var today = DateTime.UtcNow.Date;
        var countToday = await _db.MaintenanceTickets.CountAsync(t => t.CreatedAt >= today, ct);
        var ticketCode = $"TCK-{DateTime.UtcNow:yyyyMMdd}-{(countToday + 1):D3}";

        var managerId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : (tech.CreatedBy ?? Guid.Empty);

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            AnomalyId = request.AnomalyId,
            ManagerId = managerId,
            TechnicianId = request.TechnicianId,
            Status = TicketStatus.Open,
            Priority = parsedPriority,
            Description = request.Description,
            DueDate = request.DueDate,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
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
            CreatedAt = DateTime.UtcNow
        });

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
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct)
            ?? throw new NotFoundException("Maintenance ticket not found");

        if (ticket.Status != TicketStatus.Open)
        {
            throw new BusinessRuleException("TICKET_NOT_OPEN", $"Only open tickets can be (re)assigned; this ticket is '{ticket.Status}'.");
        }

        if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new BusinessRuleException("Due date cannot be in the past");
        }

        var tech = await LoadTechnicianAsync(request.TechnicianId, ct);
        var old = new { ticket.TechnicianId, ticket.DueDate };

        ticket.TechnicianId = tech.Id;
        ticket.DueDate = request.DueDate ?? ticket.DueDate;
        ticket.AssignedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

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
            CreatedAt = DateTime.UtcNow
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

        await _db.SaveChangesAsync(ct);
        return MapToDto(ticket);
    }

    /// <summary>Kỹ thuật viên đang hoạt động kèm số phiếu chưa đóng, để chọn khi giao phiếu.</summary>
    public async Task<IReadOnlyList<TechnicianSummaryDto>> GetTechniciansAsync(CancellationToken ct = default)
    {
        var technicianRoles = new[] { UserRoles.Technician.ToLower(), UserRoles.MaintenanceTechnician.ToLower() };
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
                    t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved)
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
            string.Equals(ur.Role?.RoleName, UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase));

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
        EnsureTechnicianOrAdminAccess(ticket);

        // Terminal State Guard
        if (ticket.Status == TicketStatus.Closed || ticket.Status == TicketStatus.Resolved)
        {
            throw new BusinessRuleException("Cannot modify status of an already closed ticket");
        }

        if (string.Equals(newStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            if (ticket.Status != TicketStatus.Open)
            {
                throw new BusinessRuleException($"Cannot start progress on ticket in status '{ticket.Status}'. Must be Open.");
            }
            if (ticket.TechnicianId == null)
            {
                throw new BusinessRuleException("Cannot start progress on a ticket that has no technician assigned.");
            }
            ticket.Status = TicketStatus.InProgress;
            ticket.StartedAt = DateTime.UtcNow;
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
        else if (string.Equals(newStatus, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(newStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
        {
            // Closing is the manager's acceptance step: same rules, audit and anomaly update as PUT /close.
            if (!IsManagerOrAdmin())
            {
                throw new ForbiddenException("TICKET_CLOSE_REQUIRES_MANAGER: Only a Manager or SystemAdmin can accept and close a ticket.");
            }
            return await CloseTicketAsync(ticketId, ct);
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

        EnsureTechnicianOrAdminAccess(ticket);
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

        EnsureTechnicianOrAdminAccess(ticket);
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
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Resolved)
        {
            throw new BusinessRuleException("TICKET_CLOSED", "The ticket is already closed.");
        }
    }

    private void EnsureTechnicianOrAdminAccess(MaintenanceTicket ticket)
    {
        if (IsManagerOrAdmin())
        {
            return;
        }

        if (ticket.TechnicianId != _currentUser.UserId)
        {
            throw new ForbiddenException("Technician is not assigned to this ticket");
        }
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
