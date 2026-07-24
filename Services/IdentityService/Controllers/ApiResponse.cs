namespace UavPms.IdentityService.Api.Controllers;

public record ApiResponse(bool Success, string Message, object? Data = null, object? Errors = null);
