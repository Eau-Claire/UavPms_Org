namespace UavPms.Shared.Contracts.Settings;

/// <summary>Content-Type sent to object storage, from the file extension (storage serves it back as-is).</summary>
public static class StorageContentTypes
{
    public static string For(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".tif" or ".tiff" => "image/tiff",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".avi" => "video/x-msvideo",
        ".webm" => "video/webm",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
