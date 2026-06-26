using System;

namespace UavPms.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageResult
{
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}
