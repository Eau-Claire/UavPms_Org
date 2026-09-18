using FluentValidation;
using System;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;

public class RejectReportCommandValidator : AbstractValidator<RejectReportCommand>
{
    public RejectReportCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID báo cáo không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");
    }
}
