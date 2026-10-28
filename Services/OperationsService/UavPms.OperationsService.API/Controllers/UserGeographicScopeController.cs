using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;
using UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

/// <summary>
/// Default geographic scope of a user (UserGeographicScopes). The gateway routes
/// /users/{id}/geographic-scopes here; every other /users route goes to IdentityService.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/users/{userId:guid}/geographic-scopes")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class UserGeographicScopeController : ControllerBase
{
    private readonly ISender _mediator;

    public UserGeographicScopeController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách khu vực mà người dùng được phân quyền (SystemAdmin/Manager xem mọi người, vai trò khác chỉ xem của mình)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetUserGeographicScopesQuery(userId), cancellationToken);
        return Ok(new ApiResponse(true, "Lấy phạm vi địa lý của người dùng thành công.", result));
    }

    /// <summary>
    /// Thay toàn bộ phạm vi địa lý của người dùng bằng danh sách regionIds (rỗng = bỏ hết)
    /// </summary>
    [HttpPut]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Set(Guid userId, [FromBody] SetUserGeographicScopesRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new SetUserGeographicScopesCommand(userId, request.RegionIds ?? new List<Guid>()), cancellationToken);
        return Ok(new ApiResponse(true, "Cập nhật phạm vi địa lý của người dùng thành công.", result));
    }
}

public record SetUserGeographicScopesRequest(List<Guid>? RegionIds);
