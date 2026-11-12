using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UavPms.AIInspectionService.API.Middlewares;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Middlewares;

public class GlobalExceptionHandlerTests
{
    private static async Task<(HttpStatusCode Status, string Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, exception, CancellationToken.None);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return ((HttpStatusCode)context.Response.StatusCode, body);
    }

    [Fact]
    public async Task ConflictBusinessRuleException_ShouldReturn409ConflictWithErrorCode()
    {
        var (status, body) = await InvokeAsync(new BusinessRuleException("DUPLICATE_MEDIA", "Media already exists."));

        status.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("\"errorCode\":\"DUPLICATE_MEDIA\"");
    }

    [Fact]
    public async Task NonConflictBusinessRuleException_ShouldReturn400BadRequestWithErrorCode()
    {
        var (status, body) = await InvokeAsync(new BusinessRuleException("TOWER_REQUIRED", "Tower is required."));

        status.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("\"errorCode\":\"TOWER_REQUIRED\"");
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_ShouldReturn409Conflict()
    {
        var (status, body) = await InvokeAsync(new DbUpdateConcurrencyException("concurrency conflict"));

        status.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("\"errorCode\":\"CONCURRENCY_CONFLICT\"");
    }

    [Fact]
    public async Task ValidationException_ShouldReturn400WithStandardValidationErrorCode()
    {
        var (status, body) = await InvokeAsync(new ValidationException([new ValidationFailure("Field", "Invalid")]));

        status.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("\"errorCode\":\"VALIDATION_ERROR\"");
    }
}

