namespace UavPms.OperationsService.Application.Common.DTOs;

public record PaginationMetaData(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);