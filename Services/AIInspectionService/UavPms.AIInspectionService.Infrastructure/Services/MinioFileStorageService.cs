using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.AIInspectionService.Infrastructure.Services;

/// <summary>
/// Stores edge evidence frames in MinIO (S3 API, path-style) as <c>{bucket}/{yyyy}/{MM}/{guid}_{name}</c> and returns
/// the public URL <c>{PublicBaseUrl}/{bucket}/{object}</c>; the database keeps only <c>bucket/object</c> (see StorageKeys).
/// Same layout as OperationsService's MinioFileStorageService.
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif",
        ".mp4", ".avi", ".mov", ".webm"
    };

    private static readonly SemaphoreSlim BucketLock = new(1, 1);
    private static volatile bool _bucketReady;

    private readonly IAmazonS3 _s3;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(IAmazonS3 s3, IOptions<MinioOptions> options, ILogger<MinioFileStorageService> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>S3 client for MinIO: path-style addressing, checksums only when required (older MinIO releases).</summary>
    public static IAmazonS3 CreateClient(MinioOptions options) => new AmazonS3Client(
        new BasicAWSCredentials(options.AccessKey, options.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = $"{(options.UseSsl ? "https" : "http")}://{options.Endpoint.Trim()}",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });

    public async Task<string> SaveImageAsync(Stream fileStream, string fileName)
    {
        // Only the file name part (no path traversal), allowed extension, safe characters.
        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"File extension '{extension}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");
        }
        safeFileName = Regex.Replace(safeFileName, @"[^\w\-.]", "_");

        await EnsureBucketAsync();

        var now = DateTime.UtcNow;
        var objectName = $"{now:yyyy}/{now:MM}/{Guid.NewGuid()}_{safeFileName}";
        if (fileStream.CanSeek) fileStream.Position = 0;

        // Throws AmazonS3Exception on any non-2xx answer (auth, missing bucket, quota...).
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectName,
            InputStream = fileStream,
            AutoCloseStream = false,
            ContentType = StorageContentTypes.For(safeFileName),
        });

        _logger.LogInformation("Uploaded media to MinIO: {Bucket}/{Object}", _options.Bucket, objectName);
        return $"{_options.ResolvedPublicBaseUrl}/{_options.Bucket}/{objectName}";
    }

    public async Task DeleteImageAsync(string imagePath)
    {
        var prefix = $"{_options.ResolvedPublicBaseUrl}/{_options.Bucket}/";
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var objectName = Uri.UnescapeDataString(imagePath[prefix.Length..]);
        try
        {
            await _s3.DeleteObjectAsync(_options.Bucket, objectName);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete MinIO object {Bucket}/{Object}", _options.Bucket, objectName);
        }
    }

    /// <summary>Creates the bucket on first use with anonymous read on objects (no listing).</summary>
    private async Task EnsureBucketAsync()
    {
        if (_bucketReady) return;
        await BucketLock.WaitAsync();
        try
        {
            if (_bucketReady) return;
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _options.Bucket))
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket });
                await _s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = _options.Bucket,
                    Policy = $$"""
                        {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{_options.Bucket}}/*"]}]}
                        """,
                });
                _logger.LogInformation("Created MinIO bucket {Bucket} with public read on objects", _options.Bucket);
            }
            _bucketReady = true;
        }
        finally
        {
            BucketLock.Release();
        }
    }
}
