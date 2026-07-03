using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Queries.GetMissionDetails;

public record GetMissionDetailsQuery(Guid Id) : IRequest<MissionDto>;