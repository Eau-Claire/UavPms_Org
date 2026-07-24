using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;

public record GetMissionDetailsQuery(Guid Id) : IRequest<MissionDto>;