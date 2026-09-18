using System.IO;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record ReportFileDownloadDto(Stream FileStream, string ContentType, string FileName);
