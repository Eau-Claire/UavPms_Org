using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Regions.Commands.CreateRegion;
using UavPms.OperationsService.Application.Features.Regions.Commands.UpdateRegion;
using UavPms.OperationsService.Application.Features.Regions.Commands.DeleteRegion;
using UavPms.OperationsService.Application.Features.Regions.Queries.GetRegions;
using UavPms.OperationsService.Application.Features.Regions.Queries.GetRegionById;
using UavPms.OperationsService.Application.Features.Regions.UserAssignments;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/regions")]
[ApiVersion("1.0")]
[Authorize]
public class RegionController : ControllerBase
{
    private readonly ISender _mediator;

    public RegionController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = new GetRegionsQuery(page, pageSize, search);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách vùng miền thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetRegionByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin vùng miền thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateRegionRequest request)
    {
        var command = new CreateRegionCommand(request.RegionName);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo vùng miền thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRegionRequest request)
    {
        var command = new UpdateRegionCommand(id, request.RegionName);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật vùng miền thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteRegionCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa vùng miền thành công."));
    }

    [HttpPost("{regionId:guid}/users/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> AssignUser(Guid regionId, Guid userId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new AssignUserToRegionCommand(userId, regionId), cancellationToken);
        return Ok(new ApiResponse(true, "User assigned to Region successfully."));
    }

    [HttpDelete("{regionId:guid}/users/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> RemoveUser(Guid regionId, Guid userId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveUserFromRegionCommand(userId, regionId), cancellationToken);
        return Ok(new ApiResponse(true, "User removed from Region successfully."));
    }

    [HttpGet("users/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> GetUserRegions(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserRegionsQuery(userId), cancellationToken);
        return Ok(new ApiResponse(true, "User Regions retrieved successfully.", result));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateRegionRequest(string RegionName);
public record UpdateRegionRequest(string RegionName);
