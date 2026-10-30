using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Settings;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

/// <summary>
/// Runs against a real MinIO only when MINIO_TEST_ENDPOINT / MINIO_TEST_ACCESS_KEY / MINIO_TEST_SECRET_KEY are set
/// (e.g. a local <c>minio/minio</c> container); otherwise it returns immediately.
/// </summary>
public class MinioFileStorageServiceIntegrationTests
{
    [Fact]
    public async Task Upload_IsPubliclyReadableWithContentType_ThenDelete()
    {
        var endpoint = Environment.GetEnvironmentVariable("MINIO_TEST_ENDPOINT");
        var accessKey = Environment.GetEnvironmentVariable("MINIO_TEST_ACCESS_KEY");
        var secretKey = Environment.GetEnvironmentVariable("MINIO_TEST_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            return;

        var options = new MinioOptions { Endpoint = endpoint, AccessKey = accessKey, SecretKey = secretKey, Bucket = $"it-{Guid.NewGuid():N}"[..20] };
        var client = MinioFileStorageService.CreateClient(options);
        var storage = new MinioFileStorageService(client, Options.Create(options), NullLogger<MinioFileStorageService>.Instance);
        var bytes = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();

        var url = await storage.SaveImageAsync(new MemoryStream(bytes), "pole photo.png");

        url.Should().StartWith($"http://{endpoint}/{options.Bucket}/{DateTime.UtcNow:yyyy}/");
        using var http = new HttpClient();
        using var response = await http.GetAsync(url); // anonymous: bucket created with public read
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        await storage.DeleteImageAsync(url);
        using var afterDelete = await http.GetAsync(url);
        afterDelete.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
