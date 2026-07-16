using MediatR;
using UavPms.Application.Features.Substations.DTOs;

namespace UavPms.Application.Features.Substations.Commands.CreateSubstation;

public record CreateSubstationCommand(
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    double? Latitude,
    double? Longitude) : IRequest<SubstationDto>;