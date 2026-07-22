using MediatR;
using UavPms.Application.Features.Substations.DTOs;

namespace UavPms.Application.Features.Substations.Commands.UpdateSubstation;

public record UpdateSubstationCommand(
    Guid Id,
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    double? Latitude,
    double? Longitude) : IRequest<SubstationDto>;