using System.Collections.Generic;

namespace UavPms.Application.Features.Users.DTOs;

public record PaginatedUsersResponse(
    List<UserDetailDto> Items,
    PaginationMetaData Pagination
);
