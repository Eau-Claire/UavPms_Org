using MediatR;
using UavPms.OperationsService.Application.Features.Substations.DTOs;

namespace UavPms.OperationsService.Application.Features.Substations.Queries.GetSubstationById;

public record GetSubstationByIdQuery(Guid Id) : IRequest<SubstationDto>;