using System;
using FluentValidation;

namespace UavPms.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

public class ProcessAiAnalysisResultCommandValidator : AbstractValidator<ProcessAiAnalysisResultCommand>
{
    public ProcessAiAnalysisResultCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("RequestId is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => "Completed".Equals(s, StringComparison.OrdinalIgnoreCase) || 
                       "Failed".Equals(s, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be 'Completed' or 'Failed'.");

        RuleFor(x => x.CompletedAt)
            .NotEmpty().WithMessage("CompletedAt is required.");

        When(x => "Completed".Equals(x.Status, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ModelName)
                .NotEmpty().WithMessage("ModelName is required when Status is Completed.");

            RuleFor(x => x.Detections)
                .NotNull().WithMessage("Detections must not be null when Status is Completed.");

            RuleForEach(x => x.Detections).ChildRules(detection =>
            {
                detection.RuleFor(d => d.CategoryCode)
                    .NotEmpty().WithMessage("CategoryCode is required.");

                detection.RuleFor(d => d.Confidence)
                    .InclusiveBetween(0, 1).WithMessage("Confidence must be between 0 and 1.");

                detection.RuleFor(d => d.BoundingBox)
                    .NotNull().WithMessage("BoundingBox is required.");

                detection.RuleFor(d => d.BoundingBox.X)
                    .InclusiveBetween(0, 1).WithMessage("BoundingBox X must be between 0 and 1.");

                detection.RuleFor(d => d.BoundingBox.Y)
                    .InclusiveBetween(0, 1).WithMessage("BoundingBox Y must be between 0 and 1.");

                detection.RuleFor(d => d.BoundingBox.Width)
                    .InclusiveBetween(0, 1).WithMessage("BoundingBox Width must be between 0 and 1.");

                detection.RuleFor(d => d.BoundingBox.Height)
                    .InclusiveBetween(0, 1).WithMessage("BoundingBox Height must be between 0 and 1.");
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
