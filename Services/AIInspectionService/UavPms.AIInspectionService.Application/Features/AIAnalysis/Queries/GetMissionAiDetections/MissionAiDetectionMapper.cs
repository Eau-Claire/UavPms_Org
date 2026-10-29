using System;
using System.Linq;
using System.Text.Json;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Application.Common.Utilities;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public static class MissionAiDetectionMapper
{
    public static MissionAiDetectionDto MapDetection(DetectedAnomaly anomaly)
    {
        return new MissionAiDetectionDto
        {
            Id = anomaly.Id,
            MediaId = anomaly.MediaId,
            AnalysisRunId = anomaly.AnalysisRunId,
            ExecutionLocation = anomaly.AnalysisRun?.ExecutionLocation,
            SupersededById = anomaly.SupersededById,
            AiDetectionId = anomaly.AiDetectionId,
            CategoryCode = anomaly.Category?.CategoryCode ?? string.Empty,
            Class = anomaly.Category?.CategoryName ?? anomaly.Category?.CategoryCode ?? string.Empty,
            CategoryName = anomaly.Category?.CategoryName ?? string.Empty,
            CategoryDescription = anomaly.Category?.Description ?? string.Empty,
            SeverityWeight = anomaly.Category?.SeverityWeight ?? 0,
            IsEmergencyClass = anomaly.Category?.IsEmergencyClass ?? false,
            ConfidenceScore = anomaly.ConfidenceScore,
            Confidence = anomaly.ConfidenceScore,
            FrameIndex = anomaly.FrameIndex,
            Timestamp = anomaly.FrameTimeSeconds,
            ImageUrl = string.IsNullOrWhiteSpace(anomaly.ImageUrl)
                ? anomaly.Media?.FileUrl
                : anomaly.ImageUrl,
            CropUrl = anomaly.CropUrl,
            Gps = MapGps(anomaly.Location),
            TowerId = anomaly.TowerId,
            ValidationStatus = anomaly.ValidationStatus,
            AiSource = anomaly.GetAiSource(),
            AnalystId = anomaly.AnalystId,
            AnalystNotes = anomaly.AnalystNotes,
            BoundingBox = TryParseBoundingBox(anomaly.BoundingBox),
            RawBoundingBox = anomaly.BoundingBox,
            ValidatedAt = anomaly.ValidatedAt,
            CreatedAt = anomaly.CreatedAt
        };
    }


    /// <summary>Video metadata now lives on the media row (DurationSeconds/Fps/WidthPx/HeightPx).</summary>
    public static MissionAiVideoMetadataDto? MapVideoMetadata(InspectionMedia media)
    {
        if (media.DurationSeconds == null &&
            media.Fps == null &&
            media.WidthPx == null &&
            media.HeightPx == null)
        {
            return null;
        }

        return new MissionAiVideoMetadataDto
        {
            Duration = media.DurationSeconds,
            Fps = media.Fps,
            Width = media.WidthPx,
            Height = media.HeightPx
        };
    }

    /// <summary>Derived media-level AI source: latest run model name, or "Manual" when no run produced its anomalies.</summary>
    public static string ResolveMediaAiSource(InspectionMedia media)
    {
        var latestRun = media.AnalysisRuns
            .Where(run => !run.IsDeleted)
            .OrderByDescending(run => run.CompletedAt ?? run.CreatedAt)
            .FirstOrDefault();
        if (latestRun != null)
            return string.IsNullOrWhiteSpace(latestRun.ModelName) ? latestRun.ExecutionLocation : latestRun.ModelName;

        var sources = media.DetectedAnomalies.Select(a => a.GetAiSource()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        return sources.Count == 1 ? sources[0] : string.Join(", ", sources);
    }

    private static MissionAiGpsDto? MapGps(Point? location)
    {
        if (location == null || location.IsEmpty)
        {
            return null;
        }

        // geometry(Point,4326): X = longitude, Y = latitude
        return new MissionAiGpsDto
        {
            Lat = location.Y,
            Lng = location.X
        };
    }

    private static MissionAiBoundingBoxDto? TryParseBoundingBox(string rawBoundingBox)
    {
        return DetectionMatcher.TryParse(rawBoundingBox, out var box)
            ? new MissionAiBoundingBoxDto { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height }
            : null;
    }
}
