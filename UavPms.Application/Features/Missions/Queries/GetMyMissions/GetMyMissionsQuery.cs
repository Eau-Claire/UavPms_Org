using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Queries.GetMyMissions;

public record GetMyMissionsQuery : IRequest<List<MissionDto>>;