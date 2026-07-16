using MediatR;
using System.IO;

namespace UavPms.Application.Features.Towers.Commands.ImportTowers;

public record ImportTowersCommand(Stream FileStream) : IRequest<ImportTowersResponse>;

public record ImportTowersResponse(
    bool Success,
    int ImportedCount,
    int CreateAssetsCount
);
