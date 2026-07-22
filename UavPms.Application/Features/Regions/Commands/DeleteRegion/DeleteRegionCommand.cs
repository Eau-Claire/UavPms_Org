using System;
using MediatR;

namespace UavPms.Application.Features.Regions.Commands.DeleteRegion;

public record DeleteRegionCommand(Guid Id) : IRequest;
