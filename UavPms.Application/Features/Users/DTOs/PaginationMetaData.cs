namespace UavPms.Application.Features.Users.DTOs;

public record PaginationMetaData(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);

