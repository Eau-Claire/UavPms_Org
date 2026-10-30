namespace UavPms.Shared.Contracts.Settings;

/// <summary>
/// Plan item 2.8 (backward compatible): media/proof/crop columns store a storage key
/// (e.g. <c>uav-images/abc.jpg</c>) instead of the full public URL, while code and API keep working with
/// full URLs. Services map these columns through an EF value converter built on <see cref="ToKey"/> and
/// <see cref="ToUrl"/>.
/// Anything that is not under the configured public base (local <c>/images/..</c> paths, other hosts)
/// is stored and returned unchanged. Switching to signed URLs later only changes <see cref="ToUrl"/>'s callers.
/// </summary>
public static class StorageKeys
{
    /// <summary>Supabase public object route; the key is the rest of the URL (bucket/object).</summary>
    public const string PublicObjectPath = "/storage/v1/object/public/";

    private static string? _publicBaseUrl;

    /// <summary>e.g. <c>https://xyz.supabase.co/storage/v1/object/public/</c>; null when no remote storage is configured.</summary>
    public static string? PublicBaseUrl => _publicBaseUrl;

    /// <summary>Called once at startup with the storage host (Supabase:Url); null/empty disables key mapping.</summary>
    public static void Configure(string? storageBaseUrl)
        => _publicBaseUrl = string.IsNullOrWhiteSpace(storageBaseUrl)
            ? null
            : storageBaseUrl.Trim().TrimEnd('/') + PublicObjectPath;

    /// <summary>Value written to the database.</summary>
    public static string ToKey(string url)
    {
        var baseUrl = _publicBaseUrl;
        return baseUrl != null && url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase) && url.Length > baseUrl.Length
            ? url[baseUrl.Length..]
            : url;
    }

    /// <summary>Value exposed to the application: a key becomes the full public URL.</summary>
    public static string ToUrl(string stored)
    {
        var baseUrl = _publicBaseUrl;
        return baseUrl == null || stored.Length == 0 || stored.StartsWith('/') || stored.Contains("://", StringComparison.Ordinal)
            ? stored
            : baseUrl + stored;
    }
}
