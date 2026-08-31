using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
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
                Errors: errors
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
            );
        }
        else if (exception is NotFoundException notFoundException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            apiResponse = new ApiResponse(
                Success: false,
                Message: notFoundException.Message,
                Data: null,
                Errors: null
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
            );
        }
        else if (exception is BusinessRuleException businessRuleException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            apiResponse = new ApiResponse(
                Success: false,
                Message: businessRuleException.Message,
                Data: null,
                Errors: null
            );
        }
        else
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var errorMessage = "An unexpected error occurred. Please try again later.";
            
            // Include more details in development environment
            if (_environment.IsDevelopment())
            {
                errorMessage = $"{exception.GetType().Name}: {exception.Message}";
            }
            
            apiResponse = new ApiResponse(
                Success: false,
                Message: errorMessage,
                Data: null,
                Errors: null
            );
        }

        await httpContext.Response.WriteAsJsonAsync(apiResponse, cancellationToken);
        
        return true;
    }
}

