using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Services;

public interface IReportExportService
{
    Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateReportFileAsync(
        Report report,
        IReadOnlyList<DetectedAnomaly> anomalies,
        string format,
        CancellationToken cancellationToken = default);

    Task<string> SaveReportFileAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<(Stream FileStream, string ContentType, string FileName)?> GetReportFileStreamAsync(
        string fileUrl,
        CancellationToken cancellationToken = default);
}
