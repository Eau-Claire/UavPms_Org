using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Services;

public class SupabaseFileStorageService : IFileStorageService
{
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly string _bucketName;
    private readonly ILogger<SupabaseFileStorageService> _logger;
    private static readonly HttpClient _httpClient = new HttpClient();

    public SupabaseFileStorageService(
        IConfiguration configuration,
        ILogger<SupabaseFileStorageService> logger)
    {
        _logger = logger;

        _supabaseUrl = configuration["Supabase:Url"] ?? "https://hurroumcfjmzsnzovefm.supabase.co";
        _supabaseKey = configuration["Supabase:ApiKey"] ?? string.Empty;
        _bucketName = configuration["Supabase:Bucket"] ?? "uav-images";

        // Clean up trailing slash if any
        if (_supabaseUrl.EndsWith("/"))
        {
            _supabaseUrl = _supabaseUrl.Substring(0, _supabaseUrl.Length - 1);
        }
    }

    public async Task<string> SaveImageAsync(Stream fileStream, string fileName)
    {
        if (string.IsNullOrEmpty(_supabaseKey))
        {
            _logger.LogWarning("Supabase ApiKey is empty! Falling back to returning a mock URL.");
            return $"/images/mock_{fileName}";
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{uniqueFileName}";

        _logger.LogInformation("Uploading image to Supabase Storage: {UploadUrl}", uploadUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        
        // Add Supabase authentication headers
        request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");
        request.Headers.Add("apikey", _supabaseKey);

        // Read stream into byte array
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Supabase upload failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Supabase Storage upload failed with status {response.StatusCode}: {errorBody}");
        }

        var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucketName}/{uniqueFileName}";
        _logger.LogInformation("Successfully uploaded image to Supabase. Public URL: {PublicUrl}", publicUrl);

        return publicUrl;
    }

    public async Task DeleteImageAsync(string imagePath)
    {
        if (string.IsNullOrEmpty(_supabaseKey) || string.IsNullOrEmpty(imagePath))
        {
            return;
        }

        try
        {
            // Extract filename from the public URL
            var fileName = Path.GetFileName(imagePath);
            var deleteUrl = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{fileName}";

            _logger.LogInformation("Deleting image from Supabase Storage: {DeleteUrl}", deleteUrl);

            using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");
            request.Headers.Add("apikey", _supabaseKey);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to delete image from Supabase Storage: {ErrorBody}", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred while deleting image from Supabase Storage");
        }
    }
}
