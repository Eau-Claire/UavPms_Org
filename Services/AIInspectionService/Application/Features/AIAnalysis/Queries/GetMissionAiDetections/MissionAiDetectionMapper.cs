using System;
using System.Text.Json;
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
            AssetId = anomaly.AssetId,
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
            Timestamp = anomaly.Timestamp,
            ImageUrl = string.IsNullOrWhiteSpace(anomaly.ImageUrl)
                ? anomaly.Media?.FileUrl
                : anomaly.ImageUrl,
            CropUrl = anomaly.CropUrl,
            Gps = TryParseGps(anomaly.Gps),
            TowerId = anomaly.TowerId,
            ValidationStatus = anomaly.ValidationStatus,
            AiSource = anomaly.AiSource,
            AnalystId = anomaly.AnalystId,
            AnalystNotes = anomaly.AnalystNotes,
            BoundingBox = TryParseBoundingBox(anomaly.BoundingBox),
            RawBoundingBox = anomaly.BoundingBox,
            ValidatedAt = anomaly.ValidatedAt,
            CreatedAt = anomaly.CreatedAt
        };
    }


    public static MissionAiVideoMetadataDto? MapVideoMetadata(DetectedAnomaly anomaly)
    {
        if (anomaly.VideoDuration == null &&
            anomaly.VideoFps == null &&
            anomaly.VideoWidth == null &&
            anomaly.VideoHeight == null)
        {
            return null;
        }

        return new MissionAiVideoMetadataDto
        {
            Duration = anomaly.VideoDuration,
            Fps = anomaly.VideoFps,
            Width = anomaly.VideoWidth,
            Height = anomaly.VideoHeight
        };
    }

    private static MissionAiGpsDto? TryParseGps(string? rawGps)
    {
        if (string.IsNullOrWhiteSpace(rawGps))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawGps);
            var root = document.RootElement;

            return new MissionAiGpsDto
            {
                Lat = TryGetDouble(root, "lat", out var lat) ? lat : null,
                Lng = TryGetDouble(root, "lng", out var lng) ? lng : null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MissionAiBoundingBoxDto? TryParseBoundingBox(string rawBoundingBox)
    {
        if (string.IsNullOrWhiteSpace(rawBoundingBox))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawBoundingBox);
            var root = document.RootElement;

            if (TryGetDouble(root, "x", out var x) &&
                TryGetDouble(root, "y", out var y) &&
                TryGetDouble(root, "width", out var width) &&
                TryGetDouble(root, "height", out var height))
            {
                return new MissionAiBoundingBoxDto
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                };
            }

            if (TryGetDouble(root, "x1", out var x1) &&
                TryGetDouble(root, "y1", out var y1) &&
                TryGetDouble(root, "x2", out var x2) &&
                TryGetDouble(root, "y2", out var y2))
            {
                return new MissionAiBoundingBoxDto
                {
                    X = x1,
                    Y = y1,
                    Width = Math.Max(0, x2 - x1),
                    Height = Math.Max(0, y2 - y1)
                };
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!TryGetPropertyCaseInsensitive(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDouble(out value);
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
