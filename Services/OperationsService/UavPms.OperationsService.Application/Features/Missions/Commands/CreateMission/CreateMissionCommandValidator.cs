using FluentValidation;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

public class CreateMissionCommandValidator  : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters");

        RuleFor(command => command)
            .Must(command => command.AssignedToUserId != Guid.Empty || command.InspectorId.HasValue)
            .WithMessage("Inspector is required");

        RuleFor(command => command)
            .Must(command => !string.IsNullOrWhiteSpace(command.DroneCode) || command.UavId.HasValue)
            .WithMessage("Drone is required");

        RuleFor(command => command.TargetAssetIds)
            .NotEmpty().WithMessage("MISSION_TARGET_REQUIRED");
        
        RuleFor(command => command.Status)
            .Must(status => string.IsNullOrEmpty(status) ||
                            status == "Pending" ||
                            status == "In Progress" ||
                            status == "Completed")
            .WithMessage("Status is required");
    }
}
