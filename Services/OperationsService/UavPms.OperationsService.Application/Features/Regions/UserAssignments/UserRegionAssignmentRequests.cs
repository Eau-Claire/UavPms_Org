using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Regions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Regions.UserAssignments;

public record AssignUserToRegionCommand(Guid UserId, Guid RegionId) : IRequest;
public record RemoveUserFromRegionCommand(Guid UserId, Guid RegionId) : IRequest;
public record GetUserRegionsQuery(Guid UserId) : IRequest<IReadOnlyList<RegionDto>>;

public class AssignUserToRegionCommandHandler : IRequestHandler<AssignUserToRegionCommand>
{
    private readonly IRegionRepository _regions;
    private readonly IUserRepository _users;
    private readonly IUserRegionAssignmentRepository _assignments;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    public AssignUserToRegionCommandHandler(IRegionRepository regions, IUserRepository users, IUserRegionAssignmentRepository assignments, ICurrentUserServices currentUser, IUnitOfWork unitOfWork) =>
        (_regions, _users, _assignments, _currentUser, _unitOfWork) = (regions, users, assignments, currentUser, unitOfWork);

    public async Task Handle(AssignUserToRegionCommand request, CancellationToken ct)
    {
        if (await _regions.GetByIdAsync(request.RegionId, false) == null) throw new NotFoundException("Region", request.RegionId);
        if (await _users.GetByIdAsync(request.UserId, false) == null) throw new NotFoundException("User", request.UserId);
        if (await _assignments.ExistsAsync(request.UserId, request.RegionId, ct)) throw new BusinessRuleException("User is already assigned to the Region.");
        await _assignments.AddAsync(new UserRegionAssignment { UserId = request.UserId, RegionId = request.RegionId, AssignedAt = DateTimeOffset.UtcNow, AssignedBy = _currentUser.UserId }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

public class RemoveUserFromRegionCommandHandler : IRequestHandler<RemoveUserFromRegionCommand>
{
    private readonly IUserRegionAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;
    public RemoveUserFromRegionCommandHandler(IUserRegionAssignmentRepository assignments, IUnitOfWork unitOfWork) => (_assignments, _unitOfWork) = (assignments, unitOfWork);
    public async Task Handle(RemoveUserFromRegionCommand request, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(request.UserId, request.RegionId, ct) ?? throw new NotFoundException("UserRegionAssignment", $"{request.UserId}/{request.RegionId}");
        _assignments.Remove(assignment);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

public class GetUserRegionsQueryHandler : IRequestHandler<GetUserRegionsQuery, IReadOnlyList<RegionDto>>
{
    private readonly IUserRegionAssignmentRepository _assignments;
    public GetUserRegionsQueryHandler(IUserRegionAssignmentRepository assignments) => _assignments = assignments;
    public async Task<IReadOnlyList<RegionDto>> Handle(GetUserRegionsQuery request, CancellationToken ct) =>
        (await _assignments.GetRegionsAsync(request.UserId, ct)).Select(x => new RegionDto(x.Id, x.RegionName, x.Geom?.AsText())).ToArray();
}
