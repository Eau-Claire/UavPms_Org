using FluentAssertions;
using Moq;
using UavPms.Application.Features.Users.Queries.GetUsers;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
namespace UavPms.UnitTests.Features.Users.Queries.GetUsers;

public class GetUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUsersQueryHandler _handler;

    public GetUsersQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUsersQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaginatedList_WhenCalled()
    {
        var query = new GetUsersQuery(1, 10, null);
        var mockUserList = new List<User>()
        {
            new User() { Username = "user1", Email = "1@gmail.com", UserRoles = new List<UserRole>() },
            new User() { Username = "user2", Email = "2@gmail.com", UserRoles = new List<UserRole>() },
        };
        
        _userRepositoryMock.Setup(r => r.GetUsersPagedAsync(1, 10, null))
            .ReturnsAsync((mockUserList, 2));
        
        var result = await _handler.Handle(query, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
        result.Pagination.TotalPages.Should().Be(1);
    }
}
