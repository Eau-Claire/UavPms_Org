using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

public class SupabaseFileStorageService : IFileStorageService
{
    private readonly SupabaseOptions _supabaseOptions;
    private readonly ILogger<SupabaseFileStorageService> _logger;
    private static readonly HttpClient _httpClient = new HttpClient();

    public SupabaseFileStorageService(
        IOptions<SupabaseOptions> supabaseOptions, 
        ILogger<SupabaseFileStorageService> logger)
    {
        _logger = logger;
        _supabaseOptions = supabaseOptions.Value;
    }

    public async Task<string> SaveImageAsync(Stream fileStream, string fileName)
    {
        var supabaseUrl = _supabaseOptions.Url.TrimEnd('/');
        var supabaseKey = _supabaseOptions.ApiKey;
        var bucketName = _supabaseOptions.Bucket;
        
        if (string.IsNullOrEmpty(supabaseKey))
        {
            _logger.LogWarning("Supabase ApiKey is empty! Falling back to returning a mock URL.");
            return $"/images/mock_{fileName}";
        }

        if (!FileSanitizer.IsAllowedExtension(fileName))
        {
            var extension = Path.GetExtension(fileName);
            throw new ArgumentException(
                $"File extension: {extension} is not allowed. Allowed: {string.Join(", ", FileSanitizer.DefaultAllowedExtensions)}");
        }
        
        var uniqueFileName = FileSanitizer.GenerateUniqueFileName(fileName);
        var encodedFileName = Uri.EscapeDataString(uniqueFileName);
        
        var uploadUrl = $"{supabaseUrl}/storage/v1/object/{bucketName}/{encodedFileName}";

        _logger.LogInformation("Uploading image to Supabase Storage: {UploadUrl}", uploadUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        
        // Add Supabase authentication headers
        request.Headers.Add("Authorization", $"Bearer {supabaseKey}");
        request.Headers.Add("apikey", supabaseKey);

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

        var publicUrl = $"{supabaseUrl}/storage/v1/object/public/{bucketName}/{encodedFileName}";
        _logger.LogInformation("Successfully uploaded image to Supabase. Public URL: {PublicUrl}", publicUrl);

        return publicUrl;
    }

    public async Task DeleteImageAsync(string imagePath)
    {
        var supabaseUrl = _supabaseOptions.Url.TrimEnd('/');
        var supabaseKey = _supabaseOptions.ApiKey;
        var bucketName = _supabaseOptions.Bucket;
        
        if (string.IsNullOrEmpty(supabaseKey) || string.IsNullOrEmpty(imagePath))
        {
            return;
        }

        try
        {
            // Extract filename from the public URL
            var fileName = Path.GetFileName(imagePath);
            var deleteUrl = $"{supabaseUrl}/storage/v1/object/{bucketName}/{fileName}";

            _logger.LogInformation("Deleting image from Supabase Storage: {DeleteUrl}", deleteUrl);

            using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            request.Headers.Add("Authorization", $"Bearer {supabaseKey}");
            request.Headers.Add("apikey", supabaseKey);

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
