using MediatR;
using UavPms.OperationsService.Application.Features.Substations.DTOs;

namespace UavPms.OperationsService.Application.Features.Substations.Commands.CreateSubstation;

public record CreateSubstationCommand(
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    double? Latitude,
    double? Longitude) : IRequest<SubstationDto>;