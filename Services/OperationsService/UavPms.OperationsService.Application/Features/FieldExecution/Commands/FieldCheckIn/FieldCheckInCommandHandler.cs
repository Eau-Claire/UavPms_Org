using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.FieldCheckIn;

public class FieldCheckInCommandHandler : IRequestHandler<FieldCheckInCommand, MissionCheckInResponseDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IGenericRepository<MissionCheckIn> _checkInRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public FieldCheckInCommandHandler(
        IMissionRepository missionRepository,
        ITowerRepository towerRepository,
        IGenericRepository<MissionCheckIn> checkInRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
        _checkInRepository = checkInRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<MissionCheckInResponseDto> Handle(FieldCheckInCommand request, CancellationToken cancellationToken)
    {
        if (request.Latitude < -90 || request.Latitude > 90)
            throw new BusinessRuleException("INVALID_COORDINATES", "Latitude must be between -90 and 90.");

        if (request.Longitude < -180 || request.Longitude > 180)
            throw new BusinessRuleException("INVALID_COORDINATES", "Longitude must be between -180 and 180.");

        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
            throw new NotFoundException("Mission", request.MissionId);

        var currentUserId = _currentUser.UserId;
        var assignment = mission.Assignments.FirstOrDefault(a => a.UserId == currentUserId && a.Status == MissionAssignmentStatus.Active);
        if (assignment == null)
            throw new ForbiddenException("USER_NOT_ASSIGNED: You are not assigned to this mission.");

        if (assignment != null && assignment.ResponseStatus != MissionAssignmentResponse.Accepted)
            throw new BusinessRuleException("ASSIGNMENT_NOT_ACCEPTED", "You must accept the mission assignment before checking in.");

        var existingCheckIns = await _checkInRepository.FindAsync(
            c => c.MissionId == request.MissionId && c.UserId == currentUserId && c.Status == MissionCheckInStatus.CheckedIn,
            track: false);

        if (existingCheckIns.Count > 0)
            throw new BusinessRuleException("DUPLICATE_CHECK_IN", "User has already checked in for this mission.");

        // Spatial validation: check distance to first target tower/tower <= 200m
        double? targetLat = null;
        double? targetLon = null;

        var firstTarget = mission.MissionTargets.FirstOrDefault();
        if (firstTarget != null && firstTarget.TowerId != Guid.Empty)
        {
            var tower = await _towerRepository.GetByIdAsync(firstTarget.TowerId);
            if (tower?.Geom != null)
            {
                targetLat = tower.Geom.Coordinate.Y;
                targetLon = tower.Geom.Coordinate.X;
            }
        }

        if (targetLat.HasValue && targetLon.HasValue)
        {
            var distanceMeters = CalculateGeodesicDistanceMeters(request.Latitude, request.Longitude, targetLat.Value, targetLon.Value);
            if (distanceMeters > 200.0)
            {
                throw new BusinessRuleException("CHECKIN_OUTSIDE_GEOFENCE",
                    $"Vị trí hiện trường ({request.Latitude:F5}, {request.Longitude:F5}) cách cột mục tiêu {distanceMeters:F1}m, vượt quá bán kính cho phép 200m.");
            }
        }

        var checkInLocation = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        var checkIn = new MissionCheckIn
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            UserId = currentUserId,
            CheckInLocation = checkInLocation,
            CheckedInAt = DateTime.UtcNow,
            Status = MissionCheckInStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow
        };

        await _checkInRepository.AddAsync(checkIn);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MissionCheckInResponseDto
        {
            Id = checkIn.Id,
            MissionId = checkIn.MissionId,
            UserId = checkIn.UserId,
            CheckedInAt = checkIn.CheckedInAt,
            Status = checkIn.Status.ToString(),
            CreatedAt = checkIn.CreatedAt
        };
    }

    private static double CalculateGeodesicDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return earthRadiusMeters * c;
    }
}
