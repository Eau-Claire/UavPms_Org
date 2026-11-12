using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UavPms.NotificationService.API.Controllers;
using UavPms.NotificationService.Application.Features.Notifications.Queries.GetUnreadCount;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.NotificationService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Notifications;

public class UnreadCountAndInternalAuthTests
{
    [Fact]
    public async Task GetUnreadCountQueryHandler_ShouldCountOnlyUnreadNotificationsForUser()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options, null);
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        db.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "U1", Body = "B1", ReadAt = null },
            new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "U2", Body = "B2", ReadAt = null },
            new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "R1", Body = "B3", ReadAt = DateTime.UtcNow },
            new Notification { Id = Guid.NewGuid(), UserId = otherId, Title = "O1", Body = "B4", ReadAt = null });
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);
        var handler = new GetUnreadCountQueryHandler(repo);

        var count = await handler.Handle(new GetUnreadCountQuery(userId), CancellationToken.None);

        count.Should().Be(2);
    }

    [Fact]
    public async Task NotificationController_GetUnreadCount_ShouldReturnCountForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(new GetUnreadCountQuery(userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var controller = new NotificationController(mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Inspector)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.GetUnreadCount(null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var api = ok.Value.Should().BeOfType<ApiResponse>().Subject;
        api.Success.Should().BeTrue();
        api.Data.Should().BeEquivalentTo(new { unreadCount = 5 });
    }

    [Fact]
    public void InternalServiceOrAdminAuth_ShouldRejectUnauthenticatedWithoutKey_AndAllowMatchingKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServices:SharedKey"] = "super-secret-internal-key"
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var attr = new InternalServiceOrAdminAuthAttribute();

        // 1. No JWT, no key -> 401
        var unauthContext = new AuthorizationFilterContext(
            new ActionContext(
                new DefaultHttpContext { RequestServices = services },
                new RouteData(),
                new ActionDescriptor()),
            new List<IFilterMetadata>());

        attr.OnAuthorization(unauthContext);
        unauthContext.Result.Should().BeOfType<UnauthorizedObjectResult>();

        // 2. Valid X-Internal-Key -> allowed (Result remains null)
        var validHttp = new DefaultHttpContext { RequestServices = services };
        validHttp.Request.Headers["X-Internal-Key"] = "super-secret-internal-key";
        var keyContext = new AuthorizationFilterContext(
            new ActionContext(validHttp, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        attr.OnAuthorization(keyContext);
        keyContext.Result.Should().BeNull();
    }
}

