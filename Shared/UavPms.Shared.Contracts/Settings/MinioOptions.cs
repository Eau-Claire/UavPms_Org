namespace UavPms.Shared.Contracts.Settings;

/// <summary>
/// Object storage for every media file (inspection media, maintenance proofs, edge frames).
/// Bound from the "Minio" section (env <c>Minio__Endpoint</c>, ...). Credentials come from the
/// environment only, never from appsettings or the repository.
/// </summary>
public class MinioOptions
{
    public const string SectionName = "Minio";

    /// <summary>S3 API host and port, without scheme, e.g. <c>116.118.3.58:9000</c> (not the 9001 console).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "uav-media";
    public bool UseSsl { get; set; }

    /// <summary>
    /// Base of the URLs handed to clients and the AI worker (e.g. an HTTPS reverse proxy in front of MinIO).
    /// Defaults to the S3 endpoint itself.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);

    public string ResolvedPublicBaseUrl =>
        (string.IsNullOrWhiteSpace(PublicBaseUrl) ? $"{(UseSsl ? "https" : "http")}://{Endpoint.Trim()}" : PublicBaseUrl.Trim())
        .TrimEnd('/');
}
