using System;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageResult
{
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public Guid? TowerId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string? Checksum { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public double? DurationSeconds { get; set; }
    public double? Fps { get; set; }
    /// <summary>True when the same file (same SHA-256) was already uploaded to this mission and that media is returned.</summary>
    public bool IsDuplicate { get; set; }

    public static UploadInspectionImageResult From(InspectionMedia media, bool isDuplicate = false) => new()
    {
        MediaId = media.Id,
        MissionId = media.MissionId,
        TowerId = media.TowerId,
        FileUrl = media.FileUrl,
        MediaType = media.MediaType,
        CapturedAt = media.CapturedAt,
        Checksum = media.Checksum,
        FileSizeBytes = media.FileSizeBytes,
        WidthPx = media.WidthPx,
        HeightPx = media.HeightPx,
        DurationSeconds = media.DurationSeconds,
        Fps = media.Fps,
        IsDuplicate = isDuplicate
    };
}
