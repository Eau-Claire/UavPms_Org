using System.Collections.Generic;
using UavPms.IdentityService.Application.Common.DTOs;

namespace UavPms.IdentityService.Application.Features.Users.DTOs;

public record PaginatedUsersResponse(
    List<UserDetailDto> Items,
    PaginationMetaData Pagination
);
