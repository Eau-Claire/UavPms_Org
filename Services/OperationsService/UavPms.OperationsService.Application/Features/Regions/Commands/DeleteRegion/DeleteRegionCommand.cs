using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Regions.Commands.DeleteRegion;

public record DeleteRegionCommand(Guid Id) : IRequest;
