using FluentValidation;
using UavPms.OperationsService.Application.Common.Validation;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLines;

public sealed class GetTransmissionLinesQueryValidator : AbstractValidator<GetTransmissionLinesQuery>
{
    public GetTransmissionLinesQueryValidator() => PaginationRules.Apply(this, query => query.Page, query => query.PageSize);
}
