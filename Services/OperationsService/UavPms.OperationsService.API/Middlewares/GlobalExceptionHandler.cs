using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Net;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;   
    }
    
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        ApiResponse apiResponse;

        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select( e => e.ErrorMessage).ToArray()
                );

            apiResponse = new ApiResponse(
                Success: false,
                Message: "One or more validation errors occurred.",
                Data: null,
                Errors: errors,
                ErrorCode: "VALIDATION_ERROR"
            );
        }
        else if (exception is UnauthorizedAccessException unauthorizedAccessException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            apiResponse = new ApiResponse(
                Success: false,
                Message: unauthorizedAccessException.Message,
                Data: null,
                Errors: null
                , ErrorCode: "UNAUTHORIZED"
            );
        }
        else if (exception is ForbiddenException forbiddenException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            apiResponse = new ApiResponse(false, forbiddenException.Message, ErrorCode: "FORBIDDEN");
        }
        else if (exception is NotFoundException notFoundException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            apiResponse = new ApiResponse(
                Success: false,
                Message: notFoundException.Message,
                Data: null,
                Errors: null
                , ErrorCode: "NOT_FOUND"
            );
        }
        else if (exception is KeyNotFoundException keyNotFoundException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            apiResponse = new ApiResponse(
                Success: false,
                Message: keyNotFoundException.Message,
                Data: null,
                Errors: null
                , ErrorCode: keyNotFoundException.Message.StartsWith("Mission", StringComparison.OrdinalIgnoreCase) ? "MISSION_NOT_FOUND" : "ASSET_NOT_FOUND"
            );
        }
        else if (exception is DbUpdateConcurrencyException concurrencyException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
            apiResponse = new ApiResponse(
                Success: false,
                Message: "A concurrency conflict occurred. The resource was modified by another operation.",
                Data: null,
                Errors: null,
                ErrorCode: "CONCURRENCY_CONFLICT"
            );
        }
        else if (exception is BusinessRuleException businessRuleException)
        {
            var code = !string.IsNullOrWhiteSpace(businessRuleException.Code)
                ? businessRuleException.Code
                : (businessRuleException.Message.Contains("mission inspection scope", StringComparison.OrdinalIgnoreCase)
                    ? "INVALID_MISSION_ASSET"
                    : "BUSINESS_RULE_VIOLATION");

            var isConflict = code is "INVALID_MISSION_STATE"
                or "RESOURCE_BOOKING_CONFLICT"
                or "MISSION_CONCURRENCY_CONFLICT"
                or "ASSESSMENT_CONCURRENCY_CONFLICT"
                or "IDEMPOTENCY_CONFLICT";

            httpContext.Response.StatusCode = isConflict ? (int)HttpStatusCode.Conflict : (int)HttpStatusCode.BadRequest;
            apiResponse = new ApiResponse(
                Success: false,
                Message: businessRuleException.Message,
                Data: null,
                Errors: null,
                ErrorCode: code
            );
        }
        else if (exception is InvalidOperationException invalidOperationException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
            apiResponse = new ApiResponse(
                Success: false,
                Message: invalidOperationException.Message,
                Data: null,
                Errors: null,
                ErrorCode: "INVALID_MISSION_STATE"
            );
        }
        else if (exception is InfrastructureOperationException infrastructureException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            apiResponse = new ApiResponse(false, infrastructureException.Message, ErrorCode: infrastructureException.ErrorCode);
        }
        else
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            apiResponse = new ApiResponse(
                Success: false,
                Message: "An unexpected error occurred. Please try again later.",
                Data: null,
                Errors: null
                , ErrorCode: "INTERNAL_ERROR"
            );
        }

        await httpContext.Response.WriteAsJsonAsync(apiResponse, cancellationToken);
        
        return true;
    }
}
