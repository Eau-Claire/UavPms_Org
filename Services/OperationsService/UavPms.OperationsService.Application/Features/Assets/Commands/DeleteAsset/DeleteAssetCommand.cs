using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.DeleteAssetComponent;

public record DeleteAssetComponentCommand(Guid Id) : IRequest;
