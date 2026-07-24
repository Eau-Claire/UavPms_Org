using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMyMissions;

public record GetMyMissionsQuery : IRequest<List<MissionDto>>;