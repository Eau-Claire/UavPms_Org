using System;
using System.IO;
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

namespace UavPms.OperationsService.Application.Features.Reports.Commands.GenerateReport;

public class GenerateReportCommandHandler : IRequestHandler<GenerateReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly IReportExportService _reportExportService;
    private readonly ICurrentUserServices _currentUserServices;

    public GenerateReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        IReportExportService reportExportService,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _reportExportService = reportExportService;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportDetailDto> Handle(GenerateReportCommand request, CancellationToken cancellationToken)
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
                    throw new ForbiddenException("Quản lý không có quyền xuất bản báo cáo ngoài khu vực quản lý.");
                }
            }
            else if (report.Status == ReportStatus.Draft && report.CreatedBy.HasValue && report.CreatedBy.Value != _currentUserServices.UserId)
            {
                throw new ForbiddenException("Bạn chỉ có thể xuất bản báo cáo do chính mình tạo ra.");
            }
        }

        bool isExcel = string.Equals(request.Format, "excel", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(request.Format, "xlsx", StringComparison.OrdinalIgnoreCase);
        string normalizedFormat = isExcel ? "excel" : "pdf";

        // Query linked mission IDs and their anomalies
        var missionIds = report.ReportMissions.Select(rm => rm.MissionId).Distinct().ToList();
        var anomalies = await _reportRepository.GetAnomaliesByMissionIdsAsync(missionIds);

        // Generate file bytes
        var (fileBytes, contentType, fileName) = await _reportExportService.GenerateReportFileAsync(
            report,
            anomalies,
            normalizedFormat,
            cancellationToken
        );

        // Save file to storage
        using var stream = new MemoryStream(fileBytes);
        var fileUrl = await _reportExportService.SaveReportFileAsync(stream, fileName, cancellationToken);

        // Update report entity
        if (normalizedFormat == "excel")
        {
            report.ExcelFileUrl = fileUrl;
            report.ExcelFileSize = fileBytes.LongLength;
        }
        else
        {
            report.PdfFileUrl = fileUrl;
            report.PdfFileSize = fileBytes.LongLength;
        }

        await _reportRepository.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReportDetailDto(
            report.Id,
            report.Code,
            report.Title,
            report.Type.ToString().ToLowerInvariant(),
            report.Status.ToString().ToLowerInvariant(),
            report.Description,
            report.CreatedAt,
            report.UpdatedAt,
            report.TransmissionLine != null ? new ReportNestedEntityDto(report.TransmissionLine.Id, report.TransmissionLine.LineName) : null,
            report.Substation != null ? new ReportNestedEntityDto(report.Substation.Id, report.Substation.SubstationName) : null,
            report.CreatedByUser != null ? new ReportCreatorDto(report.CreatedByUser.Id, report.CreatedByUser.FullName) : null,
            report.DefectCount,
            report.PdfFileSize,
            report.ExcelFileSize,
            report.PdfFileUrl,
            report.ExcelFileUrl,
            report.DateFrom,
            report.DateTo,
            report.RejectionReason,
            report.ApprovedBy != null ? new ReportCreatorDto(report.ApprovedBy.Id, report.ApprovedBy.FullName) : null,
            report.ApprovedAt,
            report.ReportMissions.Select(rm => new ReportMissionSummaryDto(
                rm.MissionId,
                rm.Mission?.MissionCode ?? string.Empty,
                rm.Mission?.Title ?? string.Empty,
                rm.Mission?.Status.ToString().ToLowerInvariant() ?? string.Empty,
                rm.Mission?.ScheduledStartAt
            )).ToList()
        );
    }
}
