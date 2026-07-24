using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Assets.Commands.DeleteAsset;

public record DeleteAssetCommand(Guid Id) : IRequest;
