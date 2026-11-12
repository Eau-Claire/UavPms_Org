using System.IO;
using SharedMediaMetadataExtractor = UavPms.Shared.Contracts.Media.MediaMetadataExtractor;

namespace UavPms.OperationsService.Application.Common.Utilities;

public record MediaMetadata(int? WidthPx, int? HeightPx, double? DurationSeconds, double? Fps);

/// <summary>
/// Delegates to the shared <see cref="SharedMediaMetadataExtractor"/> to avoid duplicating parser logic.
/// </summary>
public static class MediaMetadataExtractor
{
    public static string ComputeSha256(Stream stream)
        => SharedMediaMetadataExtractor.ComputeSha256(stream);

    public static MediaMetadata Extract(Stream? stream, string? contentType)
    {
        var result = SharedMediaMetadataExtractor.Extract(stream, contentType);
        return new MediaMetadata(result.WidthPx, result.HeightPx, result.DurationSeconds, result.Fps);
    }
}
