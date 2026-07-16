using MediatR;

namespace UavPms.Application.Features.Towers.Commands.ImportTowers;

public record ImporTowersCommand(Stream FileStream) : IRequest<ImportTowersResponse>;

public record ImportTowersResponse(
    bool Success,
    int ImportedCount,
    int CreateAssetsCount
);