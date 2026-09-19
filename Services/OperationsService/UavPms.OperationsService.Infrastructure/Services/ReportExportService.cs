using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

public class ReportExportService : IReportExportService
{
    private readonly string _storagePath;
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(IConfiguration configuration, ILogger<ReportExportService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var rawPath = configuration["FileStorage:ReportsPath"] ?? "uav_storage/reports";
        _storagePath = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(Directory.GetCurrentDirectory(), rawPath);

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateReportFileAsync(
        Report report,
        IReadOnlyList<DetectedAnomaly> anomalies,
        string format,
        CancellationToken cancellationToken = default)
    {
        bool isExcel = string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

        if (isExcel)
        {
            var excelBytes = GenerateExcelReport(report, anomalies);
            var excelFileName = $"{report.Code}.xlsx";
            return Task.FromResult((excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelFileName));
        }

        var pdfBytes = GeneratePdfReport(report, anomalies);
        var pdfFileName = $"{report.Code}.pdf";
        return Task.FromResult((pdfBytes, "application/pdf", pdfFileName));
    }

    private byte[] GeneratePdfReport(Report report, IReadOnlyList<DetectedAnomaly> anomalies)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(headerCol =>
                        {
                            headerCol.Item().Text("HỆ THỐNG QUẢN LÝ VẬN HÀNH UAV-PMS").Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                            headerCol.Item().Text("TỔNG CÔNG TY TRUYỀN TẢI ĐIỆN QUỐC GIA").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text($"Mã BC: {report.Code}").Bold().FontSize(9);
                            metaCol.Item().Text($"Ngày tạo: {report.CreatedAt:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    // Report Title
                    content.Item().AlignCenter().Text(report.Title.ToUpperInvariant())
                        .Bold().FontSize(15).FontColor(Colors.Blue.Darken3);

                    content.Item().PaddingTop(4).AlignCenter().Text($"Loại báo cáo: {report.Type.ToString().ToUpperInvariant()} | Trạng thái: {report.Status.ToString().ToUpperInvariant()}")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);

                    content.Item().PaddingTop(12).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);

                    // Overview section
                    content.Item().PaddingTop(10).Text("1. THÔNG TIN CHUNG").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);

                    content.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(130);
                            columns.RelativeColumn();
                            columns.ConstantColumn(130);
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Tuyến đường dây:").Bold();
                        table.Cell().Text(report.TransmissionLine?.LineName ?? "Không chỉ định");

                        table.Cell().Text("Trạm biến áp:").Bold();
                        table.Cell().Text(report.Substation?.SubstationName ?? "Không chỉ định");

                        table.Cell().Text("Người tạo:").Bold();
                        table.Cell().Text(report.CreatedByUser?.FullName ?? "N/A");

                        table.Cell().Text("Người phê duyệt:").Bold();
                        table.Cell().Text(report.ApprovedBy?.FullName ?? "Chưa phê duyệt");

                        table.Cell().Text("Khoảng thời gian:").Bold();
                        table.Cell().Text(report.DateFrom.HasValue || report.DateTo.HasValue
                            ? $"{report.DateFrom:dd/MM/yyyy} - {report.DateTo:dd/MM/yyyy}"
                            : "Toàn thời gian");

                        table.Cell().Text("Số lượng khuyết tật:").Bold();
                        table.Cell().Text($"{report.DefectCount} phát hiện").FontColor(report.DefectCount > 0 ? Colors.Red.Darken1 : Colors.Green.Darken1).Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(report.Description))
                    {
                        content.Item().PaddingTop(8).Text("Mô tả / Ghi chú:").Bold();
                        content.Item().Text(report.Description).Italic().FontColor(Colors.Grey.Darken2);
                    }

                    // Missions section
                    content.Item().PaddingTop(14).Text("2. DANH SÁCH ĐỢT BAY KHẢO SÁT LIÊN QUAN").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);

                    if (report.ReportMissions == null || !report.ReportMissions.Any())
                    {
                        content.Item().PaddingTop(4).Text("Không có đợt bay nào được liên kết với báo cáo này.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        content.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.ConstantColumn(90);
                                columns.RelativeColumn();
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(100);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("STT").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Mã đợt bay").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Tên đợt bay").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Trạng thái").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Ngày bay").Bold();
                            });

                            int index = 1;
                            foreach (var rm in report.ReportMissions)
                            {
                                var m = rm.Mission;
                                table.Cell().Padding(3).Text(index++.ToString());
                                table.Cell().Padding(3).Text(m?.MissionCode ?? "N/A");
                                table.Cell().Padding(3).Text(m?.Title ?? "N/A");
                                table.Cell().Padding(3).Text(m?.Status.ToString() ?? "N/A");
                                table.Cell().Padding(3).Text(m?.ScheduledStartAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
                            }
                        });
                    }

                    // Anomalies section
                    content.Item().PaddingTop(14).Text("3. BẢNG TỔNG HỢP KHUYẾT TẬT & BẤT THƯỜNG").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);

                    if (anomalies == null || !anomalies.Any())
                    {
                        content.Item().PaddingTop(4).Text("Không ghi nhận khuyết tật hoặc bất thường nào trong phạm vi báo cáo.").Italic().FontColor(Colors.Green.Darken2);
                    }
                    else
                    {
                        content.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.ConstantColumn(90);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("STT").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Loại khuyết tật").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Mã đợt bay").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Độ tin cậy").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Trạng thái").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Thời gian").Bold();
                            });

                            int idx = 1;
                            foreach (var a in anomalies)
                            {
                                table.Cell().Padding(3).Text(idx++.ToString());
                                table.Cell().Padding(3).Text(a.Category?.CategoryName ?? a.AiSource ?? "Bất thường thiết bị");
                                table.Cell().Padding(3).Text(a.Media?.Mission?.MissionCode ?? "N/A");
                                table.Cell().Padding(3).Text($"{a.ConfidenceScore * 100:0.0}%");
                                table.Cell().Padding(3).Text(string.IsNullOrWhiteSpace(a.ValidationStatus) ? "Chờ xác nhận" : a.ValidationStatus);
                                table.Cell().Padding(3).Text(a.CreatedAt.ToString("dd/MM/yyyy"));
                            }
                        });
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
                    col.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text($"Xuất bản từ UAV-PMS lúc {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[] GenerateExcelReport(Report report, IReadOnlyList<DetectedAnomaly> anomalies)
    {
        using var package = new ExcelPackage();

        // Sheet 1: Tổng quan
        var wsOverview = package.Workbook.Worksheets.Add("Tổng quan báo cáo");
        wsOverview.Cells["A1"].Value = "HỆ THỐNG QUẢN LÝ VẬN HÀNH UAV-PMS";
        wsOverview.Cells["A1"].Style.Font.Bold = true;
        wsOverview.Cells["A1"].Style.Font.Size = 14;

        wsOverview.Cells["A2"].Value = $"BÁO CÁO: {report.Title}";
        wsOverview.Cells["A2"].Style.Font.Bold = true;
        wsOverview.Cells["A2"].Style.Font.Size = 12;

        wsOverview.Cells["A4"].Value = "Mã báo cáo:";
        wsOverview.Cells["B4"].Value = report.Code;
        wsOverview.Cells["A5"].Value = "Loại báo cáo:";
        wsOverview.Cells["B5"].Value = report.Type.ToString();
        wsOverview.Cells["A6"].Value = "Trạng thái:";
        wsOverview.Cells["B6"].Value = report.Status.ToString();
        wsOverview.Cells["A7"].Value = "Tuyến đường dây:";
        wsOverview.Cells["B7"].Value = report.TransmissionLine?.LineName ?? "Không chỉ định";
        wsOverview.Cells["A8"].Value = "Trạm biến áp:";
        wsOverview.Cells["B8"].Value = report.Substation?.SubstationName ?? "Không chỉ định";
        wsOverview.Cells["A9"].Value = "Người tạo:";
        wsOverview.Cells["B9"].Value = report.CreatedByUser?.FullName ?? "N/A";
        wsOverview.Cells["A10"].Value = "Người phê duyệt:";
        wsOverview.Cells["B10"].Value = report.ApprovedBy?.FullName ?? "Chưa phê duyệt";
        wsOverview.Cells["A11"].Value = "Ngày tạo:";
        wsOverview.Cells["B11"].Value = report.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        wsOverview.Cells["A12"].Value = "Tổng số khuyết tật:";
        wsOverview.Cells["B12"].Value = report.DefectCount;
        wsOverview.Cells["A13"].Value = "Mô tả:";
        wsOverview.Cells["B13"].Value = report.Description ?? string.Empty;

        for (int r = 4; r <= 13; r++)
        {
            wsOverview.Cells[$"A{r}"].Style.Font.Bold = true;
        }
        wsOverview.Cells["A:B"].AutoFitColumns();

        // Sheet 2: Danh sách đợt bay
        var wsMissions = package.Workbook.Worksheets.Add("Danh sách đợt bay");
        string[] missionHeaders = { "STT", "Mã đợt bay", "Tên đợt bay", "Trạng thái", "Ngày bay" };
        for (int c = 0; c < missionHeaders.Length; c++)
        {
            wsMissions.Cells[1, c + 1].Value = missionHeaders[c];
            wsMissions.Cells[1, c + 1].Style.Font.Bold = true;
            wsMissions.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            wsMissions.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightSteelBlue);
        }

        int mRow = 2;
        if (report.ReportMissions != null)
        {
            int mIdx = 1;
            foreach (var rm in report.ReportMissions)
            {
                var m = rm.Mission;
                wsMissions.Cells[mRow, 1].Value = mIdx++;
                wsMissions.Cells[mRow, 2].Value = m?.MissionCode ?? "N/A";
                wsMissions.Cells[mRow, 3].Value = m?.Title ?? "N/A";
                wsMissions.Cells[mRow, 4].Value = m?.Status.ToString() ?? "N/A";
                wsMissions.Cells[mRow, 5].Value = m?.ScheduledStartAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
                mRow++;
            }
        }
        wsMissions.Cells["A:E"].AutoFitColumns();

        // Sheet 3: Danh sách khuyết tật
        var wsAnomalies = package.Workbook.Worksheets.Add("Bảng kê khuyết tật");
        string[] anomalyHeaders = { "STT", "Loại khuyết tật", "Mã đợt bay", "Độ tin cậy (%)", "Trạng thái xác nhận", "Thời gian phát hiện", "Ghi chú" };
        for (int c = 0; c < anomalyHeaders.Length; c++)
        {
            wsAnomalies.Cells[1, c + 1].Value = anomalyHeaders[c];
            wsAnomalies.Cells[1, c + 1].Style.Font.Bold = true;
            wsAnomalies.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            wsAnomalies.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
        }

        int aRow = 2;
        if (anomalies != null)
        {
            int aIdx = 1;
            foreach (var a in anomalies)
            {
                wsAnomalies.Cells[aRow, 1].Value = aIdx++;
                wsAnomalies.Cells[aRow, 2].Value = a.Category?.CategoryName ?? a.AiSource ?? "Khuyết tật thiết bị";
                wsAnomalies.Cells[aRow, 3].Value = a.Media?.Mission?.MissionCode ?? "N/A";
                wsAnomalies.Cells[aRow, 4].Value = a.ConfidenceScore * 100;
                wsAnomalies.Cells[aRow, 5].Value = string.IsNullOrWhiteSpace(a.ValidationStatus) ? "Chờ xác nhận" : a.ValidationStatus;
                wsAnomalies.Cells[aRow, 6].Value = a.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                wsAnomalies.Cells[aRow, 7].Value = a.AnalystNotes ?? string.Empty;
                aRow++;
            }
        }
        wsAnomalies.Cells["A:G"].AutoFitColumns();

        return package.GetAsByteArray();
    }

    public async Task<string> SaveReportFileAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        safeFileName = Regex.Replace(safeFileName, @"[^\w\-.]", "_");
        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var filePath = Path.Combine(_storagePath, uniqueFileName);

        var resolvedPath = Path.GetFullPath(filePath);
        var resolvedStorage = Path.GetFullPath(_storagePath);
        if (!resolvedPath.StartsWith(resolvedStorage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attempted path traversal detected.");
        }

        await using var outputStream = new FileStream(filePath, FileMode.Create);
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }
        await fileStream.CopyToAsync(outputStream, cancellationToken);

        _logger.LogInformation("Saved report export file to {FilePath}", filePath);

        var encodedFileName = Uri.EscapeDataString(uniqueFileName);
        return $"/reports/{encodedFileName}";
    }

    public Task<(Stream FileStream, string ContentType, string FileName)?> GetReportFileStreamAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        var rawFileName = Path.GetFileName(fileUrl);
        var decodedFileName = Uri.UnescapeDataString(rawFileName);
        var filePath = Path.Combine(_storagePath, decodedFileName);

        var resolvedPath = Path.GetFullPath(filePath);
        var resolvedStorage = Path.GetFullPath(_storagePath);
        if (!resolvedPath.StartsWith(resolvedStorage, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Attempted path traversal in GetReportFileStreamAsync: {FileUrl}", fileUrl);
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" or ".xls" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<(Stream, string, string)?>((stream, contentType, decodedFileName));
    }
}
