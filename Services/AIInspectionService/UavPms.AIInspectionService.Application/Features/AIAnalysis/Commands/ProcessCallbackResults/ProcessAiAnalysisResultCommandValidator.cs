using System;
using FluentValidation;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

public class ProcessAiAnalysisResultCommandValidator : AbstractValidator<ProcessAiAnalysisResultCommand>
{
    public ProcessAiAnalysisResultCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("RequestId is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => "Processing".Equals(s, StringComparison.OrdinalIgnoreCase) ||
                       "Completed".Equals(s, StringComparison.OrdinalIgnoreCase) ||
                       "Failed".Equals(s, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be 'Processing', 'Completed' or 'Failed'.");

        RuleFor(x => x.CompletedAt)
            .NotEmpty().WithMessage("CompletedAt is required.");

        RuleFor(x => x.MediaId).NotEmpty().WithMessage("MediaId is required.");
        RuleFor(x => x.MissionId).NotEmpty().WithMessage("MissionId is required.");
        RuleFor(x => x.AssetId).NotEmpty().WithMessage("AssetId is required.");

        When(x => "Completed".Equals(x.Status, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ModelName)
                .NotEmpty().WithMessage("ModelName is required when Status is Completed.");

            RuleFor(x => x.Detections)
                .NotNull().WithMessage("Detections must not be null when Status is Completed.");

            RuleForEach(x => x.Detections).ChildRules(detection =>
            {
                detection.RuleFor(d => d.Id)
                    .NotEmpty().WithMessage("Detection Id is required for callback idempotency.");

                detection.RuleFor(d => d.CategoryCode)
                    .NotEmpty().WithMessage("CategoryCode is required.");

                detection.RuleFor(d => d.Confidence)
                    .InclusiveBetween(0, 1).WithMessage("Confidence must be between 0 and 1.");

                detection.RuleFor(d => d.BoundingBox)
                    .NotNull().WithMessage("BoundingBox is required.");

                detection.RuleFor(d => d.BoundingBox.X)
                    .GreaterThanOrEqualTo(0).LessThan(1).WithMessage("BoundingBox X must be at least 0 and less than 1.");

                detection.RuleFor(d => d.BoundingBox.Y)
                    .GreaterThanOrEqualTo(0).LessThan(1).WithMessage("BoundingBox Y must be at least 0 and less than 1.");

                detection.RuleFor(d => d.BoundingBox.Width)
                    .GreaterThan(0).LessThanOrEqualTo(1).WithMessage("BoundingBox Width must be greater than 0 and at most 1.");

                detection.RuleFor(d => d.BoundingBox.Height)
                    .GreaterThan(0).LessThanOrEqualTo(1).WithMessage("BoundingBox Height must be greater than 0 and at most 1.");

                detection.RuleFor(d => d.BoundingBox)
                    .Must(box => box == null || (box.X + box.Width <= 1 && box.Y + box.Height <= 1))
                    .WithMessage("BoundingBox must remain within normalized media bounds.");

                detection.RuleFor(d => d.FrameIndex)
                    .GreaterThanOrEqualTo(0).When(d => d.FrameIndex.HasValue);
                detection.RuleFor(d => d.Timestamp)
                    .GreaterThanOrEqualTo(0).When(d => d.Timestamp.HasValue);
                detection.RuleFor(d => d.TimestampMs)
                    .GreaterThanOrEqualTo(0).When(d => d.TimestampMs.HasValue);
                detection.RuleFor(d => d.Gps!.Lat)
                    .InclusiveBetween(-90, 90).When(d => d.Gps?.Lat.HasValue == true);
                detection.RuleFor(d => d.Gps!.Lng)
                    .InclusiveBetween(-180, 180).When(d => d.Gps?.Lng.HasValue == true);
            });
        });

        When(x => "Failed".Equals(x.Status, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ErrorCode)
                .NotEmpty().WithMessage("ErrorCode is required when Status is Failed.");

            RuleFor(x => x.ErrorMessage)
                .NotEmpty().WithMessage("ErrorMessage is required when Status is Failed.");
        });
    }
}
