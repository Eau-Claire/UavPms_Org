using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.FlightControllers;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class FlightControllerService : IFlightControllerService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;

    public FlightControllerService(ApplicationDbContext db, ICurrentUserServices current)
    {
        _db = db;
        _current = current;
    }

    public async Task<FlightControllerResolveResponseDto> ResolveByUidAsync(string uid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return new FlightControllerResolveResponseDto(
                Registered: false,
                FlightControllerUid: string.Empty,
                Drone: null,
                Binding: null
            );
        }

        var cleanUid = uid.Trim().ToUpperInvariant();

        var binding = await _db.DroneFlightControllers
            .Include(x => x.Drone)
            .FirstOrDefaultAsync(x => x.FlightControllerUid == cleanUid && x.UnpairedAt == null && !x.IsDeleted, ct);

        if (binding == null || binding.Drone == null || binding.Drone.IsDeleted)
        {
            return new FlightControllerResolveResponseDto(
                Registered: false,
                FlightControllerUid: cleanUid,
                Drone: null,
                Binding: null
            );
        }

        // Update LastSeenAt per specification
        binding.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var droneDto = new FlightControllerDroneDto(
            Id: binding.Drone.Id,
            Code: binding.Drone.UavCode,
            Name: string.IsNullOrWhiteSpace(binding.Drone.Model) ? binding.Drone.UavCode : $"{binding.Drone.UavCode} ({binding.Drone.Model})",
            Model: binding.Drone.Model,
            OperationalStatus: binding.Drone.OperationalStatus.ToString(),
            TechnicalHealth: binding.Drone.TechnicalHealth.ToString()
        );

        var bindingDto = MapToBindingDto(binding);

        return new FlightControllerResolveResponseDto(
            Registered: true,
            FlightControllerUid: cleanUid,
            Drone: droneDto,
            Binding: bindingDto
        );
    }

    public async Task<FlightControllerBindingDto> PairFlightControllerAsync(Guid droneId, PairFlightControllerRequest request, CancellationToken ct = default)
    {
        await RequireTechnicianOrManager(ct);

        if (string.IsNullOrWhiteSpace(request.FlightControllerUid))
        {
            throw new BusinessRuleException("FC_UID_REQUIRED", "Mã Flight Controller UID không được để trống.");
        }

        var cleanUid = request.FlightControllerUid.Trim().ToUpperInvariant();

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        var now = DateTime.UtcNow;

        // BR-02 & BR-03: Deactivate any currently active binding on this drone
        var existingDroneBindings = await _db.DroneFlightControllers
            .Where(x => x.DroneId == droneId && x.UnpairedAt == null && !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var b in existingDroneBindings)
        {
            b.UnpairedAt = now;
        }

        // BR-01: Deactivate any currently active binding of this FlightControllerUid on ANY other drone
        var existingUidBindings = await _db.DroneFlightControllers
            .Where(x => x.FlightControllerUid == cleanUid && x.UnpairedAt == null && !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var b in existingUidBindings)
        {
            b.UnpairedAt = now;
        }

        // Create new active binding
        var newBinding = new DroneFlightController
        {
            DroneId = drone.Id,
            FlightControllerUid = cleanUid,
            BoardIdentifier = request.BoardIdentifier?.Trim(),
            FirmwareName = string.IsNullOrWhiteSpace(request.FirmwareName) ? "Betaflight" : request.FirmwareName.Trim(),
            FirmwareVersion = request.FirmwareVersion?.Trim(),
            PairedAt = now,
            LastSeenAt = now
        };

        _db.DroneFlightControllers.Add(newBinding);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = "DroneFlightControllers",
            RecordId = newBinding.Id,
            ActionType = "FLIGHT_CONTROLLER_PAIRED",
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new
            {
                DroneId = drone.Id,
                DroneCode = drone.UavCode,
                FlightControllerUid = cleanUid,
                BoardIdentifier = request.BoardIdentifier,
                FirmwareName = request.FirmwareName,
                FirmwareVersion = request.FirmwareVersion,
                PairedAt = now
            })
        });

        await _db.SaveChangesAsync(ct);

        return MapToBindingDto(newBinding);
    }

    public async Task<bool> UnpairFlightControllerAsync(Guid droneId, CancellationToken ct = default)
    {
        await RequireTechnicianOrManager(ct);

        var activeBinding = await _db.DroneFlightControllers
            .FirstOrDefaultAsync(x => x.DroneId == droneId && x.UnpairedAt == null && !x.IsDeleted, ct);

        if (activeBinding == null)
            return false;

        var now = DateTime.UtcNow;
        activeBinding.UnpairedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = "DroneFlightControllers",
            RecordId = activeBinding.Id,
            ActionType = "FLIGHT_CONTROLLER_UNPAIRED",
            OldValues = JsonSerializer.Serialize(new
            {
                activeBinding.DroneId,
                activeBinding.FlightControllerUid,
                activeBinding.PairedAt
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                UnpairedAt = now,
                IsActive = false
            })
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<FlightControllerBindingDto?> GetActiveBindingForDroneAsync(Guid droneId, CancellationToken ct = default)
    {
        var binding = await _db.DroneFlightControllers
            .FirstOrDefaultAsync(x => x.DroneId == droneId && x.UnpairedAt == null && !x.IsDeleted, ct);

        return binding == null ? null : MapToBindingDto(binding);
    }

    public async Task<IReadOnlyList<FlightControllerBindingDto>> GetBindingHistoryForDroneAsync(Guid droneId, CancellationToken ct = default)
    {
        var list = await _db.DroneFlightControllers
            .Where(x => x.DroneId == droneId && !x.IsDeleted)
            .OrderByDescending(x => x.PairedAt)
            .ToListAsync(ct);

        return list.Select(MapToBindingDto).ToList();
    }

    private static FlightControllerBindingDto MapToBindingDto(DroneFlightController entity)
    {
        return new FlightControllerBindingDto(
            Id: entity.Id,
            DroneId: entity.DroneId,
            FlightControllerUid: entity.FlightControllerUid,
            BoardIdentifier: entity.BoardIdentifier,
            FirmwareName: entity.FirmwareName,
            FirmwareVersion: entity.FirmwareVersion,
            PairedAt: entity.PairedAt,
            UnpairedAt: entity.UnpairedAt,
            LastSeenAt: entity.LastSeenAt,
            IsActive: entity.UnpairedAt == null
        );
    }

    private async Task RequireTechnicianOrManager(CancellationToken ct)
    {
        if (!_current.IsAuthenticated)
            throw new ForbiddenException("Vui lòng đăng nhập để thực hiện thao tác này.");

        var hasRole = _current.Roles.Any(r =>
            r.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Technician", StringComparison.OrdinalIgnoreCase));

        if (!hasRole)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .SingleOrDefaultAsync(u => u.Id == _current.UserId, ct);

            hasRole = user?.UserRoles.Any(r =>
                r.Role != null && (
                    r.Role.RoleName.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
                    r.Role.RoleName.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
                    r.Role.RoleName.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase) ||
                    r.Role.RoleName.Equals("Technician", StringComparison.OrdinalIgnoreCase))) ?? false;
        }

        if (!hasRole)
            throw new ForbiddenException("Chỉ Kỹ thuật viên (Technician) hoặc Quản lý (Manager) mới có quyền ghép nối Flight Controller.");
    }
}
