namespace UavPms.Shared.Contracts.Settings;

/// <summary>
/// Plan item 2.8 (backward compatible): media/proof/crop columns store a storage key
/// <c>bucket/object</c> (e.g. <c>uav-media/2026/10/abc.jpg</c>) instead of the full URL, while code and API keep
/// working with full URLs. Services map these columns through an EF value converter built on <see cref="ToKey"/>
/// and <see cref="ToUrl"/>.
/// The object store is MinIO, addressed path-style: <c>{PublicBaseUrl}/{bucket}/{object}</c>, so the key is the
/// URL path after the base. Anything outside the configured base (local <c>/images/..</c> paths, legacy hosts)
/// is stored and returned unchanged. Switching to signed URLs later only changes <see cref="ToUrl"/>'s callers.
/// </summary>
public static class StorageKeys
{
    private static string? _baseUrl;

    /// <summary>e.g. <c>http://116.118.3.58:9000/</c>; null when no object store is configured.</summary>
    public static string? BaseUrl => _baseUrl;

    /// <summary>Called once at startup with the public object-store URL (Minio:PublicBaseUrl); null/empty disables key mapping.</summary>
    public static void Configure(string? publicBaseUrl)
        => _baseUrl = string.IsNullOrWhiteSpace(publicBaseUrl) ? null : publicBaseUrl.Trim().TrimEnd('/') + "/";

    /// <summary>Value written to the database.</summary>
    public static string ToKey(string url)
    {
        var baseUrl = _baseUrl;
        return baseUrl != null && url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase) && url.Length > baseUrl.Length
            ? url[baseUrl.Length..]
            : url;
    }

    /// <summary>Value exposed to the application: a key becomes the full public URL.</summary>
    public static string ToUrl(string stored)
    {
        var baseUrl = _baseUrl;
        return baseUrl == null || stored.Length == 0 || stored.StartsWith('/') || stored.Contains("://", StringComparison.Ordinal)
            ? stored
            : baseUrl + stored;
    }
}
