namespace UavPms.OperationsService.Application.Common.Options;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    
    public string AlertImagesPath { get; init; } = "uav_storage/images";
    public long MaxFileSizeBytes { get; init;} = 50 * 1024 * 1024; // Default: 50MB
}