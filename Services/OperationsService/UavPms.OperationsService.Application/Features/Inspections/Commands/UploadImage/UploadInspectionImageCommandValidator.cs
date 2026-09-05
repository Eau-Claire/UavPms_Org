using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandValidator : AbstractValidator<UploadInspectionImageCommand>
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/webp", "image/png", "image/tiff", "video/mp4" };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".webp", ".png", ".tif", ".tiff", ".mp4" };
    
    private readonly long _maxFileSizeBytes;

    public UploadInspectionImageCommandValidator(IConfiguration? configuration = null)
    {
        _maxFileSizeBytes = long.TryParse(configuration?["InspectionMedia:MaxFileSizeBytes"], out var configuredLimit)
            ? configuredLimit
            : 50L * 1024 * 1024;
        RuleFor(x => x.MissionId).NotEmpty().WithMessage("Mission ID is required.");
        
        RuleFor(x => x.AssetId).NotEmpty().WithMessage("Asset ID is required.");
        
        RuleFor(x => x.FileStream).NotEmpty().WithMessage("File stream is required.");

        RuleFor(x => x.FileStream).NotNull().WithMessage("File stream cannot be null.")
             .Must(stream => stream != null && stream.Length > 0).WithMessage("Image file is required.")
             .Must(stream => stream != null && stream.Length <= _maxFileSizeBytes)
             .WithMessage($"File size exceeds the {_maxFileSizeBytes / 1024 / 1024}MB limit.");
         
        RuleFor(x => x.ContentType).NotEmpty().WithMessage("Content type is required.")
             .Must(ct => !string.IsNullOrEmpty(ct) && AllowedContentTypes.Contains(ct.ToLower()))
             .WithMessage("Invalid file type. Allowed: JPEG, PNG, WebP, TIFF, MP4.");

        RuleFor(x => x.FileName).NotEmpty()
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage("Invalid file extension. Allowed: JPEG, PNG, WebP, TIFF, MP4.");

        RuleFor(x => x)
            .Must(x => ExtensionMatchesMimeType(x.FileName, x.ContentType))
            .WithMessage("File extension does not match the declared MIME type.");

        RuleFor(x => x)
            .Must(x => InspectionMediaFileValidator.IsValid(x.FileStream, x.ContentType))
            .WithMessage("The file signature is invalid or the media cannot be decoded.");

        RuleFor(x => x.CapturedAt)
            .Must(value => value != default && value.ToUniversalTime() <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Capture timestamp is invalid.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Latitude and longitude must be supplied together.");
    }

    private static bool ExtensionMatchesMimeType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "image/webp" => extension == ".webp",
            "image/tiff" => extension is ".tif" or ".tiff",
            "video/mp4" => extension == ".mp4",
            _ => false
        };
    }
}
