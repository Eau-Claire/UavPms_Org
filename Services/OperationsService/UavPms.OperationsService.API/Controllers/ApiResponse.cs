namespace UavPms.OperationsService.API.Controllers;

public record ApiResponse(bool Success, string Message, object? Data = null, object? Errors = null, string? ErrorCode = null);
