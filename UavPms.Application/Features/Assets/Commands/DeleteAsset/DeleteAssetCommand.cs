using System;
using MediatR;

namespace UavPms.Application.Features.Assets.Commands.DeleteAsset;

public record DeleteAssetCommand(Guid Id) : IRequest;
