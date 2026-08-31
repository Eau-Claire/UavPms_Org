using FluentValidation;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

public class CreateMissionCommandValidator  : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters");

        RuleFor(command => command.InspectorId).NotEmpty();
        RuleFor(command => command.RegionId).NotEmpty();
        RuleFor(command => command.UavId).NotEmpty();
        RuleFor(command => command.TargetTowerIds).NotNull();
        
        RuleFor(command => command.Status)
            .Must(status => string.IsNullOrEmpty(status) ||
                            status == "Pending" ||
                            status == "In Progress" ||
                            status == "Completed")
            .WithMessage("Status is required");
    }
}
