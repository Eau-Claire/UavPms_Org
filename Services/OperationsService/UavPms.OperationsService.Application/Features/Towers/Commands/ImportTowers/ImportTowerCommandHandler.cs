using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OfficeOpenXml;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Commands.ImportTowers;

public class ImportTowerCommandHandler : IRequestHandler<ImportTowersCommand, ImportTowersResponse>
{
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAssetComponentRepository _assetRepository;
    private readonly ITransmissionLineRepository _transmissionLineRepository;

    public ImportTowerCommandHandler(
        ITowerRepository towerRepository,
        IUnitOfWork unitOfWork,
        IAssetComponentRepository assetRepository,
        ITransmissionLineRepository transmissionLineRepository)
    {
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
        _assetRepository = assetRepository;
        _transmissionLineRepository = transmissionLineRepository;
    }
    
    public async  Task<ImportTowersResponse> Handle(ImportTowersCommand request, CancellationToken cancellationToken)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        int importedCount = 0;
        int createdAssetsCount = 0;

        using var package = new ExcelPackage(request.FileStream);

        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet == null)
        {
            throw new ArgumentException("Tệp Excel trống hoặc không hợp lệ");
        }
        
        int rowCount = worksheet.Dimension.Rows;
        
        // Bộc toàn bộ ác insert vào 1 DB Transaction   
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            for (int row = 2; row <= rowCount; row++) // Dòng 1 là các Headers
            {
                var lineIdString = worksheet.Cells[row, 1].Value?.ToString();
                var towerCode = worksheet.Cells[row, 2].Value?.ToString();
                var latString = worksheet.Cells[row, 3].Value?.ToString();
                var lngString = worksheet.Cells[row, 4].Value?.ToString();

                // Xác thực dữ liệu ở mức cơ bản cho từng dòng
                if (string.IsNullOrEmpty(lineIdString) || string.IsNullOrEmpty(towerCode) ||
                    !Guid.TryParse(lineIdString, out var lineId) ||
                    !double.TryParse(latString, out double lat) ||
                    !double.TryParse(lngString, out double lng))
                {
                    continue;
                }

                var lineExists = await _transmissionLineRepository.GetByIdAsync(lineId);
                if (lineExists == null || lineExists.IsDeleted)
                {
                    continue; // Bỏ qua nếu không khớp
                }

                var geom = geometryFactory.CreatePoint(new Coordinate(lng, lat));

                var tower = new Tower
                {
                    Id = Guid.NewGuid(),
                    LineAssetId = lineId,
                    TowerCode = towerCode,
                    Geom = (Point)geom,
                    CreatedAt = DateTime.UtcNow,
                };

                await _towerRepository.AddAsync(tower);
                importedCount++;

                var assetTypes = new[]
                {
                    "Insulator", "Cable", "Tower Structure", "Vibration Damper"
                };
                
                var prefixes = new[]
                {
                    "INS", "CBL", "STR", "DMP"
                };

                for (int i = 0; i < 4; i++)
                {
                    var asset = new AssetComponent
                    {
                        Id = Guid.NewGuid(),
                        TowerId = tower.Id,
                        ComponentType = assetTypes[i],
                        ComponentCode = $"{prefixes[i]}-{towerCode}-01",
                        Status = "Operational",
                        CreatedAt = DateTime.UtcNow,
                    };

                    await _assetRepository.AddAsync(asset);
                    createdAssetsCount++;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        
        return new ImportTowersResponse(true, importedCount,  createdAssetsCount);
    }
}
