namespace UavPms.Application.Common.DTOs;

public record PaginationMetaData(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);