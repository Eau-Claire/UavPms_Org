using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class ReportExportServiceTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly ReportExportService _service;

    public ReportExportServiceTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), "uav_test_reports_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStorageDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:ReportsPath"] = _testStorageDir
            })
            .Build();

        _service = new ReportExportService(config, NullLogger<ReportExportService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testStorageDir))
        {
            try
            {
                Directory.Delete(_testStorageDir, true);
            }
            catch
            {
                // Best effort cleanup in tests
            }
        }
    }

    [Fact]
    public async Task GenerateReportFileAsync_ShouldGenerateValidPdf_WhenFormatIsPdf()
    {
        // Arrange
        var report = new Report
        {
            Id = Guid.NewGuid(),
            Code = "BC-2026-0099",
            Title = "Báo cáo kiểm tra định kỳ ĐZ 500kV Thường Tín - Phố Nối",
            Type = ReportType.Periodic,
            Status = ReportStatus.Approved,
            Description = "Kiểm tra tình trạng cột và sứ cách điện",
            Region = new Region { Name = "Khu vực Thường Tín - Phố Nối", Code = "TT-PN" },
            CreatedByUser = new User { FullName = "Nguyễn Văn Kỹ Thuật" },
            ApprovedBy = new User { FullName = "Trần Văn Quản Lý" },
            ReportMissions = new List<ReportMission>
            {
                new ReportMission
                {
                    MissionId = Guid.NewGuid(),
                    Mission = new Mission
                    {
                        MissionCode = "MS-2026-01",
                        Title = "Bay quét đường dây đợt 1",
                        Status = MissionStatus.Completed,
                        PlannedStart = DateTime.UtcNow.AddDays(-2)
                    }
                }
            }
        };

        var anomalies = new List<DetectedAnomaly>
        {
            new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                Category = new DefectCategory { CategoryName = "Vỡ bát sứ cách điện", SeverityWeight = 3.5 },
                ConfidenceScore = 0.94,
                ValidationStatus = "Confirmed",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Tower = new Tower { TowerCode = "T-571-01" },
                Media = new InspectionMedia
                {
                    Mission = new Mission { MissionCode = "MS-2026-01" }
                }
            }
        };

        // Act
        var (fileBytes, contentType, fileName) = await _service.GenerateReportFileAsync(report, anomalies, "pdf");

        // Assert
        fileBytes.Should().NotBeNullOrEmpty();
        contentType.Should().Be("application/pdf");
        fileName.Should().Be("BC-2026-0099.pdf");

        // PDF starts with %PDF
        var header = Encoding.ASCII.GetString(fileBytes, 0, 4);
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateReportFileAsync_ShouldGenerateValidExcel_WhenFormatIsExcel()
    {
        // Arrange
        var report = new Report
        {
            Id = Guid.NewGuid(),
            Code = "BC-2026-0098",
            Title = "Báo cáo khuyết tật nhiệt ảnh",
            Type = ReportType.Thermal,
            Status = ReportStatus.Pending,
            Region = new Region { Name = "Khu vực Hà Đông" }
        };

        var anomalies = new List<DetectedAnomaly>
        {
            new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                Category = new DefectCategory { CategoryName = "Phát nhiệt mối nối tiếp xúc" },
                ConfidenceScore = 0.88,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var (fileBytes, contentType, fileName) = await _service.GenerateReportFileAsync(report, anomalies, "excel");

        // Assert
        fileBytes.Should().NotBeNullOrEmpty();
        contentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileName.Should().Be("BC-2026-0098.xlsx");

        // Excel file (ZIP archive) starts with PK (0x50, 0x4B)
        fileBytes[0].Should().Be(0x50);
        fileBytes[1].Should().Be(0x4B);
    }

    [Fact]
    public async Task GenerateReportFileFromSnapshotAsync_ShouldGenerateValidPdf_WhenSnapshotProvided()
    {
        // Arrange
        var snapshot = new ReportSnapshot
        {
            ReportId = Guid.NewGuid(),
            Code = "BC-2026-FROZEN",
            Title = "Báo cáo đóng băng định kỳ",
            Type = "Periodic",
            Status = "Approved",
            RegionName = "Khu vực Tây Bắc",
            ApprovedByName = "Trần Trưởng Phòng",
            TotalAnomalies = 1,
            Missions = new List<ReportSnapshotMission>
            {
                new() { MissionId = Guid.NewGuid(), MissionCode = "MS-FROZEN-01", Title = "Bay kiểm tra tuyến A", Status = "Completed" }
            },
            Anomalies = new List<ReportSnapshotAnomaly>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CategoryName = "Ăn mòn ty néo",
                    TowerCode = "T-501",
                    ConfidenceScore = 0.91,
                    ValidationStatus = "Confirmed",
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        // Act
        var (fileBytes, contentType, fileName) = await _service.GenerateReportFileFromSnapshotAsync(snapshot, "pdf");

        // Assert
        fileBytes.Should().NotBeNullOrEmpty();
        contentType.Should().Be("application/pdf");
        fileName.Should().Be("BC-2026-FROZEN.pdf");
        var header = Encoding.ASCII.GetString(fileBytes, 0, 4);
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateReportFileFromSnapshotAsync_ShouldGenerateValidExcel_WhenSnapshotJsonProvided()
    {
        // Arrange
        var snapshot = new ReportSnapshot
        {
            ReportId = Guid.NewGuid(),
            Code = "BC-2026-EXCEL-FROZEN",
            Title = "Báo cáo Excel từ snapshot JSON",
            Type = "Specialized",
            Status = "Approved",
            RegionName = "Khu vực Đông Bắc",
            TotalAnomalies = 0
        };
        var snapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Act
        var (fileBytes, contentType, fileName) = await _service.GenerateReportFileFromSnapshotAsync(snapshotJson, "excel");

        // Assert
        fileBytes.Should().NotBeNullOrEmpty();
        contentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileName.Should().Be("BC-2026-EXCEL-FROZEN.xlsx");
        fileBytes[0].Should().Be(0x50);
        fileBytes[1].Should().Be(0x4B);
    }
}
