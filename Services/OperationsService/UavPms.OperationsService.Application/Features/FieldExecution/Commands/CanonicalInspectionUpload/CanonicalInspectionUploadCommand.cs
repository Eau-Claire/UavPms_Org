using System;
using System.IO;
using MediatR;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.CanonicalInspectionUpload;

public class CanonicalInspectionUploadCommand : IRequest<UploadInspectionImageResult>
{
    public Guid MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? TowerId { get; set; }
    public DateTime CapturedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
