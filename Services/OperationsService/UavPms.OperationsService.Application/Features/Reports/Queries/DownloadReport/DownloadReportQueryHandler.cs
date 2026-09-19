using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.DownloadReport;

public class DownloadReportQueryHandler : IRequestHandler<DownloadReportQuery, ReportFileDownloadDto>
{
    private readonly IReportRepository _reportRepository;
    private readonly IReportExportService _reportExportService;
    private readonly ICurrentUserServices _currentUserServices;

    public DownloadReportQueryHandler(
        IReportRepository reportRepository,
        IReportExportService reportExportService,
        ICurrentUserServices currentUserServices)
    {
        _reportRepository = reportRepository;
        _reportExportService = reportExportService;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportFileDownloadDto> Handle(DownloadReportQuery request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        // Authorization check
        bool isAdmin = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        bool isManager = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            if (isManager)
            {
                bool canManage = await _reportRepository.CanUserManageReportAsync(_currentUserServices.UserId, report);
                if (!canManage)
                {
                    throw new ForbiddenException("Quản lý không có quyền tải báo cáo ngoài khu vực quản lý.");
                }
            }
            else if (report.Status == ReportStatus.Draft && report.CreatedBy.HasValue && report.CreatedBy.Value != _currentUserServices.UserId)
            {
                throw new ForbiddenException("Bạn chỉ có thể tải báo cáo do chính mình tạo ra.");
            }
        }

        bool isExcel = string.Equals(request.Format, "excel", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(request.Format, "xlsx", StringComparison.OrdinalIgnoreCase);

        string? fileUrl = isExcel ? report.ExcelFileUrl : report.PdfFileUrl;
        string formatName = isExcel ? "EXCEL" : "PDF";

        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new NotFoundException($"Tệp báo cáo định dạng {formatName} chưa được tạo. Vui lòng tạo tệp trước khi tải.");
        }

        var fileResult = await _reportExportService.GetReportFileStreamAsync(fileUrl, cancellationToken);
        if (fileResult == null)
        {
            throw new NotFoundException($"Tệp báo cáo định dạng {formatName} không tồn tại trên hệ thống lưu trữ.");
        }

        var (stream, contentType, fileName) = fileResult.Value;
        return new ReportFileDownloadDto(stream, contentType, fileName);
    }
}
