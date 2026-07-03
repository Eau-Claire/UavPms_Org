using FluentValidation;

namespace UavPms.Application.Features.Missions.Commands.CreateMission;

public class CreateMissionCommandValidator  : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters");

        RuleFor(command => command.RouteData)
            .NotEmpty().WithMessage("RouteData is required");

        RuleFor(command => command.AssignedToUserId)
            .NotEmpty().WithMessage("AssignedToUserId is required");

        RuleFor(command => command.DroneCode)
            .NotEmpty().WithMessage("DroneCode is required");
        
        RuleFor(command => command.Status)
            .Must(status => string.IsNullOrEmpty(status) ||
                            status == "Pending" ||
                            status == "In Progress" ||
                            status == "Completed")
            .WithMessage("Status is required");
    }
}