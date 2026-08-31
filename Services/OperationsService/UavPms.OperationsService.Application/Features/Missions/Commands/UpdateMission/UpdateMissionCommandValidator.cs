using FluentValidation;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;

public class UpdateMissionCommandValidator : AbstractValidator<UpdateMissionCommand>
{
    public UpdateMissionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.InspectorId).NotEmpty();
        RuleFor(x => x.UavId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(status => status == "Pending" || status == "In Progress" || status == "Completed")
            .WithMessage("Invalid mission status");
    }
}
