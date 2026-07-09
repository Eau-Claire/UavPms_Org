using System.Collections.Generic;
using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Users.DTOs;

public record PaginatedUsersResponse(
    List<UserDetailDto> Items,
    PaginationMetaData Pagination
);
