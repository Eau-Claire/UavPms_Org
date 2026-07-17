using MediatR;
using UavPms.Application.Features.Substations.DTOs;

namespace UavPms.Application.Features.Substations.Queries.GetSubstationById;

public record GetSubstationByIdQuery(Guid Id) : IRequest<SubstationDto>;