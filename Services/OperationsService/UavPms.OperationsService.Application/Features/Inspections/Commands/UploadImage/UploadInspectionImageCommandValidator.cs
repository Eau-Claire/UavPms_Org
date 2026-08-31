using FluentValidation;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandValidator : AbstractValidator<UploadInspectionImageCommand>
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/webp", "image/png", "image/tiff", "video/mp4" };
    
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    public UploadInspectionImageCommandValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty().WithMessage("Mission ID is required.");
        
        RuleFor(x => x.TowerId).NotEmpty().WithMessage("Tower ID is required.");
        
        RuleFor(x => x.FileStream).NotEmpty().WithMessage("File stream is required.");

        RuleFor(x => x.FileStream).NotNull().WithMessage("File stream cannot be null.")
             .Must(stream => stream != null && stream.Length > 0).WithMessage("Image file is required.")
             .Must(stream => stream != null && stream.Length <= MaxFileSizeBytes)
             .WithMessage($"File size exceeds the {MaxFileSizeBytes / 1024 / 1024}MB limit.");
         
        RuleFor(x => x.ContentType).NotEmpty().WithMessage("Content type is required.")
             .Must(ct => !string.IsNullOrEmpty(ct) && AllowedContentTypes.Contains(ct.ToLower()))
             .WithMessage("Invalid file type. Allowed: JPEG, PNG, WebP, TIFF, MP4.");
    }
}
