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
        await RequireTechnicianOrAdmin(ct);

        if (string.IsNullOrWhiteSpace(request.FlightControllerUid))
        {
            throw new BusinessRuleException("FC_UID_REQUIRED", "Mã Flight Controller UID không được để trống.");
        }

        var cleanUid = request.FlightControllerUid.Trim().ToUpperInvariant();

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        await DroneRegionScope.EnsureAsync(_db, _current, drone, ct);

        // BRL-02: UID đang gắn ở Drone khác thì không tự gỡ hay chiếm (kể cả SystemAdmin, kể cả cùng vùng);
        // phải DELETE ở Drone cũ trước. Thông báo không nêu mã Drone cũ để không lộ Drone ngoài vùng của người gọi.
        var uidActiveOnAnotherDrone = await _db.DroneFlightControllers
            .AnyAsync(x => x.FlightControllerUid == cleanUid && x.UnpairedAt == null && !x.IsDeleted && x.DroneId != droneId, ct);
        if (uidActiveOnAnotherDrone)
            throw new BusinessRuleException("FC_ALREADY_PAIRED", "Flight Controller đang gắn với Drone khác; cần ngắt kết nối (DELETE) ở Drone cũ trước.");

        var now = DateTime.UtcNow;

        // BR-02 & BR-03: Deactivate any currently active binding on this drone
        var existingDroneBindings = await _db.DroneFlightControllers
            .Where(x => x.DroneId == droneId && x.UnpairedAt == null && !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var b in existingDroneBindings)
        {
            b.UnpairedAt = now;
        }

        // Create new active binding
        var newBinding = new DroneFlightController
        {
            DroneId = drone.Id,
            FlightControllerUid = cleanUid,
            BoardIdentifier = request.BoardIdentifier?.Trim(),
            // Không truyền firmware thì để trống, tuyệt đối không tự điền tên firmware.
            FirmwareName = string.IsNullOrWhiteSpace(request.FirmwareName) ? null : request.FirmwareName.Trim(),
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
        await RequireTechnicianOrAdmin(ct);
        await EnsureDroneInScopeAsync(droneId, ct);

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
        await EnsureDroneInScopeAsync(droneId, ct);

        var binding = await _db.DroneFlightControllers
            .FirstOrDefaultAsync(x => x.DroneId == droneId && x.UnpairedAt == null && !x.IsDeleted, ct);

        return binding == null ? null : MapToBindingDto(binding);
    }

    public async Task<IReadOnlyList<FlightControllerBindingDto>> GetBindingHistoryForDroneAsync(Guid droneId, CancellationToken ct = default)
    {
        await EnsureDroneInScopeAsync(droneId, ct);

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

    /// <summary>Đọc binding FC chỉ khi Drone thuộc vùng của người gọi (SystemAdmin bỏ qua); không có Drone thì 404.</summary>
    private async Task EnsureDroneInScopeAsync(Guid droneId, CancellationToken ct)
    {
        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        await DroneRegionScope.EnsureAsync(_db, _current, drone, ct);
    }

    /// <summary>Ghép/ngắt FC thuộc nhóm ghi BIST/FC nên chỉ Technician và SystemAdmin (Manager chỉ đọc).</summary>
    private async Task RequireTechnicianOrAdmin(CancellationToken ct)
    {
        if (!_current.IsAuthenticated)
            throw new ForbiddenException("Vui lòng đăng nhập để thực hiện thao tác này.");

        var hasRole = _current.Roles.Any(r =>
            r.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Technician, StringComparison.OrdinalIgnoreCase));

        if (!hasRole)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .SingleOrDefaultAsync(u => u.Id == _current.UserId, ct);

            hasRole = user?.UserRoles.Any(r =>
                r.Role != null && (
                    r.Role.RoleName.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
                    r.Role.RoleName.Equals(UserRoles.Technician, StringComparison.OrdinalIgnoreCase))) ?? false;
        }

        if (!hasRole)
            throw new ForbiddenException("Chỉ Kỹ thuật viên (Technician) hoặc SystemAdmin mới có quyền ghép nối Flight Controller.");
    }
}
