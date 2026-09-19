using FluentValidation;
using System;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;

public class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề báo cáo không được để trống.")
            .MaximumLength(255).WithMessage("Tiêu đề báo cáo không được vượt quá 255 ký tự.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Loại báo cáo không được để trống.")
            .Must(BeAValidReportType).WithMessage("Loại báo cáo không hợp lệ. Các giá trị hợp lệ: defect, periodic, thermal, corridor.");
    }

    private static bool BeAValidReportType(string type)
    {
        return Enum.TryParse<ReportType>(type, true, out _);
    }
}
