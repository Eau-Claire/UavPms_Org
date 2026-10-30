using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Stores media in MinIO (S3 API, path-style) as <c>{bucket}/{yyyy}/{MM}/{guid}_{name}</c> and returns the public
/// URL <c>{PublicBaseUrl}/{bucket}/{object}</c>; the database keeps only <c>bucket/object</c> (see StorageKeys).
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
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

    public async Task<string> SaveImageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!FileSanitizer.IsAllowedExtension(fileName))
        {
            throw new ArgumentException(
                $"File extension: {Path.GetExtension(fileName)} is not allowed. Allowed: {string.Join(", ", FileSanitizer.DefaultAllowedExtensions)}");
        }

        await EnsureBucketAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var objectName = $"{now:yyyy}/{now:MM}/{FileSanitizer.GenerateUniqueFileName(fileName)}";
        if (fileStream.CanSeek) fileStream.Position = 0;

        // Throws AmazonS3Exception on any non-2xx answer (auth, missing bucket, quota...).
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectName,
            InputStream = fileStream,
            AutoCloseStream = false,
            ContentType = StorageContentTypes.For(fileName),
        }, cancellationToken);

        _logger.LogInformation("Uploaded media to MinIO: {Bucket}/{Object}", _options.Bucket, objectName);
        return $"{_options.ResolvedPublicBaseUrl}/{_options.Bucket}/{objectName}";
    }

    public async Task DeleteImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var prefix = $"{_options.ResolvedPublicBaseUrl}/{_options.Bucket}/";
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var objectName = Uri.UnescapeDataString(imagePath[prefix.Length..]);
        try
        {
            await _s3.DeleteObjectAsync(_options.Bucket, objectName, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete MinIO object {Bucket}/{Object}", _options.Bucket, objectName);
        }
    }

    /// <summary>
    /// Creates the bucket on first use with anonymous read on objects (no listing), because clients and the
    /// AI worker open the returned URLs directly. An existing bucket's policy is left to the operator.
    /// </summary>
    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady) return;
        await BucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady) return;
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _options.Bucket))
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
                await _s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = _options.Bucket,
                    Policy = $$"""
                        {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{_options.Bucket}}/*"]}]}
                        """,
                }, cancellationToken);
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
