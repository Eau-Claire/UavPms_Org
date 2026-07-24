namespace UavPms.NotificationService.API.Controllers;

public record ApiResponse(bool Success, string Message, object? Data = null, object? Errors = null);
