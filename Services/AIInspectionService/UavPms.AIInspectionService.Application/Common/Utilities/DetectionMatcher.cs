using System.Text.Json;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Application.Common.Utilities;

/// <summary>Bounding box as top-left corner (X, Y) and size, kept exactly as stored for {x, y, width, height}.</summary>
public readonly record struct DetectionBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>All coordinates within [0, 1]: the box is relative to the frame size.</summary>
    public bool IsNormalized => X >= 0 && Y >= 0 && Right <= 1 && Bottom <= 1;

    public DetectionBox Scale(double width, double height) => new(X * width, Y * height, Width * width, Height * height);
}

/// <summary>
/// Decides whether a cloud detection is the same defect as an earlier edge detection on the same media,
/// so the edge row can be marked <see cref="DetectedAnomaly.SupersededById"/>.
/// </summary>
public static class DetectionMatcher
{
    public const double MinIoU = 0.5;
    public const double MaxFrameGapSeconds = 1.0;

    /// <summary>
    /// Same category (checked by the caller) and, when both are known:
    /// frame times at most <see cref="MaxFrameGapSeconds"/> apart and IoU of the boxes at least <see cref="MinIoU"/>.
    /// A missing box, or boxes in different units that the media size cannot reconcile, falls back to the
    /// category-only rule so an edge result is never kept beside its cloud replacement for lack of geometry.
    /// </summary>
    public static bool IsSameDefect(DetectedAnomaly edge, DetectedAnomaly cloud, int? mediaWidthPx, int? mediaHeightPx)
    {
        if (edge.FrameTimeSeconds.HasValue && cloud.FrameTimeSeconds.HasValue &&
            Math.Abs(edge.FrameTimeSeconds.Value - cloud.FrameTimeSeconds.Value) > MaxFrameGapSeconds)
        {
            return false;
        }

        if (!TryParse(edge.BoundingBox, out var edgeBox) || !TryParse(cloud.BoundingBox, out var cloudBox))
        {
            return true;
        }

        if (edgeBox.IsNormalized != cloudBox.IsNormalized)
        {
            if (mediaWidthPx is not > 0 || mediaHeightPx is not > 0)
            {
                return true;
            }
            if (edgeBox.IsNormalized) edgeBox = edgeBox.Scale(mediaWidthPx.Value, mediaHeightPx.Value);
            else cloudBox = cloudBox.Scale(mediaWidthPx.Value, mediaHeightPx.Value);
        }

        return IoU(edgeBox, cloudBox) >= MinIoU;
    }

    public static double IoU(DetectionBox a, DetectionBox b)
        => BoundingBoxCalculations.CalculateIoU(a.X, a.Y, a.Right, a.Bottom, b.X, b.Y, b.Right, b.Bottom);

    /// <summary>Parses the stored JSON: {x, y, width, height} (cloud) or {x1, y1, x2, y2} (edge), case-insensitive.</summary>
    public static bool TryParse(string? json, out DetectionBox box)
    {
        box = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetDouble(root, "x", out var x) && TryGetDouble(root, "y", out var y) &&
                TryGetDouble(root, "width", out var width) && TryGetDouble(root, "height", out var height))
            {
                box = new DetectionBox(x, y, width, height);
                return true;
            }

            if (TryGetDouble(root, "x1", out var x1) && TryGetDouble(root, "y1", out var y1) &&
                TryGetDouble(root, "x2", out var x2) && TryGetDouble(root, "y2", out var y2))
            {
                box = new DetectionBox(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.TryGetDouble(out value),
                JsonValueKind.String => double.TryParse(property.Value.GetString(), out value),
                _ => false
            };
        }
        return false;
    }
}
