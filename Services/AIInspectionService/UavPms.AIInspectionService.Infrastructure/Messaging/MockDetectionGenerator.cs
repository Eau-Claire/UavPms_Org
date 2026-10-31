using System;
using System.Collections.Generic;
using System.Linq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

/// <summary>
/// Builds the demo detections returned by <see cref="MockAIAnalysisConsumer"/>.
/// Results are seeded by the media id, so the same file always gets the same detections, and each media gets
/// 1..MaxDetections defects of different categories with normalized (0..1) boxes. Emergency categories are
/// picked for about <see cref="MockDetectionOptions.EmergencyRate"/> of the media, so not every upload raises an alert.
/// </summary>
public static class MockDetectionGenerator
{
    private const int VideoFps = 30;

    public static List<DetectionDto> Generate(
        Guid mediaId,
        bool isVideo,
        IReadOnlyCollection<DefectCategory> categories,
        MockDetectionOptions options)
    {
        if (categories.Count == 0) return [];

        var random = new Random(Seed(mediaId));
        var ordered = categories.OrderBy(c => c.Id).ToList();

        var fixedCategory = string.IsNullOrWhiteSpace(options.CategoryCode)
            ? null
            : ordered.FirstOrDefault(c => c.CategoryCode.Equals(options.CategoryCode, StringComparison.OrdinalIgnoreCase));
        if (fixedCategory != null)
        {
            return [Detection(0, 1, fixedCategory, options.Confidence ?? 0.92, random, isVideo)];
        }

        var emergency = ordered.Where(c => c.IsEmergencyClass).ToList();
        var regular = ordered.Where(c => !c.IsEmergencyClass).ToList();
        if (regular.Count == 0) regular = ordered;

        var count = random.Next(1, Math.Max(1, options.MaxDetections) + 1);
        var picked = new List<DefectCategory>(count);
        if (emergency.Count > 0 && random.NextDouble() < options.EmergencyRate)
        {
            picked.Add(emergency[random.Next(emergency.Count)]);
        }

        // Prefer distinct categories; repeat only when there are fewer categories than detections.
        var pool = regular.Except(picked).OrderBy(_ => random.Next()).ToList();
        while (picked.Count < count)
        {
            if (pool.Count == 0) pool = regular.OrderBy(_ => random.Next()).ToList();
            picked.Add(pool[0]);
            pool.RemoveAt(0);
        }

        return picked
            .Select((category, index) => Detection(index, picked.Count, category, options.Confidence ?? 0.62 + random.NextDouble() * 0.35, random, isVideo))
            .ToList();
    }

    private static DetectionDto Detection(int index, int count, DefectCategory category, double confidence, Random random, bool isVideo)
    {
        // One vertical band per detection keeps the boxes apart.
        var band = 1.0 / count;
        var width = Math.Min(band * 0.8, 0.12 + random.NextDouble() * 0.18);
        var height = 0.12 + random.NextDouble() * 0.2;
        var x = index * band + random.NextDouble() * (band - width);
        var y = 0.1 + random.NextDouble() * (0.8 - height);

        int? frameIndex = isVideo ? index * VideoFps * 3 + random.Next(VideoFps) : null;
        return new DetectionDto
        {
            Id = $"mock-{index + 1}",
            CategoryCode = category.CategoryCode,
            ClassName = category.CategoryName,
            Confidence = Math.Round(Math.Clamp(confidence, 0, 1), 3),
            BoundingBox = new BoundingBoxDto
            {
                X = Math.Round(x, 4),
                Y = Math.Round(y, 4),
                Width = Math.Round(width, 4),
                Height = Math.Round(height, 4)
            },
            FrameIndex = frameIndex,
            TimestampMs = frameIndex * 1000 / VideoFps
        };
    }

    private static int Seed(Guid mediaId)
    {
        var bytes = mediaId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4) ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
    }
}

public sealed record MockDetectionOptions(
    string? CategoryCode = null,
    double? Confidence = null,
    int MaxDetections = 3,
    double EmergencyRate = 0.2);
