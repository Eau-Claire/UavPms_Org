using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.IdentityService.Application.Features.Users.Queries.GetAssignableUsers;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.UnitTests.Features.Users.Queries.GetAssignableUsers;

public class GetUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetAssignableUsersQueryHandler _handler;
    
    public GetUsersQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetAssignableUsersQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyActiveInspectors_WhenCalled()
    {
        var inspectors = new List<User>
        {
            new User { Id = Guid.NewGuid(), Username = "active_inspector", FullName = "Active Inspector", Email = "active@uavpms.com", Status = "Active" },
            new User { Id = Guid.NewGuid(), Username = "inactive_inspector", FullName = "Inactive Inspector", Email = "inactive@uavpms.com", Status = "Suspended" }
        };

        _userRepositoryMock.Setup(repo => repo.GetUsersByRoleAsync("Inspector"))
            .ReturnsAsync(inspectors);

        var query = new GetAssignableUsersQuery();
        
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Username.Should().Be("active_inspector");
        result[0].FullName.Should().Be("Active Inspector");
        result[0].Email.Should().Be("active@uavpms.com");
    }
}