using FluentValidation;
using System;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;

public class UpdateReportCommandValidator : AbstractValidator<UpdateReportCommand>
{
    public UpdateReportCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID báo cáo không được để trống.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề báo cáo không được để trống.")
            .MaximumLength(255).WithMessage("Tiêu đề báo cáo không được vượt quá 255 ký tự.");
    }
}
