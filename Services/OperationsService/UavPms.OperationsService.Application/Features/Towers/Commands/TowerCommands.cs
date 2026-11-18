using System.Globalization;
using System.Text;
using FluentValidation;
using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OfficeOpenXml;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Towers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Commands;

public record CreateTowerCommand(
    Guid RegionId,
    string TowerCode,
    string? PoleType,
    string? Address,
    double Latitude,
    double Longitude) : IRequest<TowerDto>;

public record UpdateTowerCommand(
    Guid Id,
    Guid RegionId,
    string TowerCode,
    string? PoleType,
    string? Address,
    double Latitude,
    double Longitude) : IRequest<TowerDto>;

public record DeleteTowerCommand(Guid Id) : IRequest;

/// <summary>Excel/CSV: cột 1 = mã khu vực (Regions.Code) hoặc RegionId, 2 = mã cột, 3 = vĩ độ, 4 = kinh độ, 5 = loại cột, 6 = địa chỉ.</summary>
public record ImportTowersCommand(Stream FileStream, string? FileName = null) : IRequest<ImportTowersResponse>;

public record ImportTowersResponse(bool Success, int ImportedCount, int SkippedCount);

public sealed class CreateTowerCommandValidator : AbstractValidator<CreateTowerCommand>
{
    public CreateTowerCommandValidator()
    {
        RuleFor(x => x.RegionId).NotEmpty();
        RuleFor(x => x.TowerCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PoleType).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class UpdateTowerCommandValidator : AbstractValidator<UpdateTowerCommand>
{
    public UpdateTowerCommandValidator()
    {
        RuleFor(x => x.RegionId).NotEmpty();
        RuleFor(x => x.TowerCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PoleType).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

internal static class TowerGeometry
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static Point Point(double latitude, double longitude) => Factory.CreatePoint(new Coordinate(longitude, latitude));
}

public class CreateTowerCommandHandler : IRequestHandler<CreateTowerCommand, TowerDto>
{
    private readonly IRegionRepository _regionRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTowerCommandHandler(IRegionRepository regionRepository, ITowerRepository towerRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TowerDto> Handle(CreateTowerCommand request, CancellationToken cancellationToken)
    {
        var region = await _regionRepository.GetByIdAsync(request.RegionId);
        if (region == null || region.IsDeleted)
            throw new NotFoundException("Region", request.RegionId);

        var code = request.TowerCode.Trim();
        if ((await _towerRepository.FindAsync(t => t.TowerCode == code)).Count > 0)
            throw new BusinessRuleException($"Tower code '{code}' already exists.");

        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            RegionId = request.RegionId,
            TowerCode = code,
            PoleType = request.PoleType,
            Address = request.Address,
            Geom = TowerGeometry.Point(request.Latitude, request.Longitude),
            Region = region
        };

        await _towerRepository.AddAsync(tower);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return TowerDto.From(tower);
    }
}

public class UpdateTowerCommandHandler : IRequestHandler<UpdateTowerCommand, TowerDto>
{
    private readonly IRegionRepository _regionRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTowerCommandHandler(IRegionRepository regionRepository, ITowerRepository towerRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TowerDto> Handle(UpdateTowerCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.Id);
        if (tower == null || tower.IsDeleted)
            throw new NotFoundException("Tower", request.Id);

        var region = await _regionRepository.GetByIdAsync(request.RegionId);
        if (region == null || region.IsDeleted)
            throw new NotFoundException("Region", request.RegionId);

        var code = request.TowerCode.Trim();
        if ((await _towerRepository.FindAsync(t => t.TowerCode == code && t.Id != request.Id)).Count > 0)
            throw new BusinessRuleException($"Tower code '{code}' already exists.");

        tower.RegionId = request.RegionId;
        tower.Region = region;
        tower.TowerCode = code;
        tower.PoleType = request.PoleType;
        tower.Address = request.Address;
        tower.Geom = TowerGeometry.Point(request.Latitude, request.Longitude);

        await _towerRepository.UpdateAsync(tower);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return TowerDto.From(tower);
    }
}

public class DeleteTowerCommandHandler : IRequestHandler<DeleteTowerCommand>
{
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTowerCommandHandler(ITowerRepository towerRepository, IUnitOfWork unitOfWork)
    {
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteTowerCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.Id);
        if (tower == null || tower.IsDeleted)
            throw new NotFoundException("Tower", request.Id);

        tower.IsDeleted = true;
        tower.DeletedAt = DateTime.UtcNow;
        await _towerRepository.UpdateAsync(tower);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public class ImportTowersCommandHandler : IRequestHandler<ImportTowersCommand, ImportTowersResponse>
{
    private readonly IRegionRepository _regionRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportTowersCommandHandler(IRegionRepository regionRepository, ITowerRepository towerRepository, IUnitOfWork unitOfWork)
    {
        _regionRepository = regionRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportTowersResponse> Handle(ImportTowersCommand request, CancellationToken cancellationToken)
    {
        var rawRows = new List<(string? RegionKey, string? TowerCode, double? Lat, double? Lng, string? PoleType, string? Address)>();
        var isCsv = !string.IsNullOrEmpty(request.FileName) && request.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        if (isCsv)
        {
            using var reader = new StreamReader(request.FileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string? line;
            bool isHeader = true;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }
                var cols = ParseCsvLine(line);
                if (cols.Count >= 4)
                {
                    rawRows.Add((
                        cols.ElementAtOrDefault(0)?.Trim(),
                        cols.ElementAtOrDefault(1)?.Trim(),
                        TryParseDouble(cols.ElementAtOrDefault(2)),
                        TryParseDouble(cols.ElementAtOrDefault(3)),
                        cols.ElementAtOrDefault(4)?.Trim(),
                        cols.ElementAtOrDefault(5)?.Trim()
                    ));
                }
            }
        }
        else
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(request.FileStream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault()
                ?? throw new ArgumentException("Tệp Excel trống hoặc không hợp lệ");
            var rowCount = worksheet.Dimension?.Rows ?? 0;

            for (var row = 2; row <= rowCount; row++) // Dòng 1 là tiêu đề
            {
                rawRows.Add((
                    worksheet.Cells[row, 1].Value?.ToString()?.Trim(),
                    worksheet.Cells[row, 2].Value?.ToString()?.Trim(),
                    TryParseDouble(worksheet.Cells[row, 3].Value?.ToString()),
                    TryParseDouble(worksheet.Cells[row, 4].Value?.ToString()),
                    worksheet.Cells[row, 5].Value?.ToString()?.Trim(),
                    worksheet.Cells[row, 6].Value?.ToString()?.Trim()
                ));
            }
        }

        var regions = await _regionRepository.GetAllAsync();
        var existingCodes = (await _towerRepository.GetAllAsync()).Select(t => t.TowerCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var r in rawRows)
            {
                var regionKey = r.RegionKey;
                var towerCode = r.TowerCode;
                var region = regions.FirstOrDefault(reg => !reg.IsDeleted
                    && (string.Equals(reg.Code, regionKey, StringComparison.OrdinalIgnoreCase)
                        || (Guid.TryParse(regionKey, out var regionId) && reg.Id == regionId)));

                if (region == null || string.IsNullOrEmpty(towerCode) || existingCodes.Contains(towerCode)
                    || !r.Lat.HasValue || !r.Lng.HasValue)
                {
                    skipped++;
                    continue;
                }

                await _towerRepository.AddAsync(new Tower
                {
                    Id = Guid.NewGuid(),
                    RegionId = region.Id,
                    TowerCode = towerCode,
                    PoleType = r.PoleType,
                    Address = r.Address,
                    Geom = TowerGeometry.Point(r.Lat.Value, r.Lng.Value)
                });
                existingCodes.Add(towerCode);
                imported++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return new ImportTowersResponse(true, imported, skipped);
    }

    private static double? TryParseDouble(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var clean = input.Trim().Replace(',', '.');
        if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            return val;
        if (double.TryParse(input.Trim(), out var valLocal))
            return valLocal;
        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line)) return result;

        char delimiter = line.Contains(';') && !line.Contains(',') ? ';' : ',';
        var inQuotes = false;
        var token = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    token.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(token.ToString().Trim());
                token.Clear();
            }
            else
            {
                token.Append(c);
            }
        }
        result.Add(token.ToString().Trim());
        return result;
    }
}
