using FluentAssertions;
using Moq;
using UavPms.Application.Features.Users.Queries.GetMyProfile;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.UnitTests.Features.Users.Queries.GetMyProfile;

public class GetMyProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _handler = new GetMyProfileQueryHandler(_mockUserRepo.Object);
    }
    
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_IfUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var query = new GetMyProfileQuery(userId);

        _mockUserRepo.Setup(u =>
                u.GetByIdWithRolesAsync(userId))
            .ReturnsAsync((User?)null);
        
        Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);
        
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"User not found or inactive");
    }
    
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_IfUserIsInactive()
    {
        var userId = Guid.NewGuid();
        var query = new GetMyProfileQuery(userId);
        var inactiveUser = new User
        {
            Id = userId,
            Username = "test",
            Email = "test@user.com",
            FullName = "test",
            Status = "Inactive"
        };
        
        _mockUserRepo.Setup(u => u.GetByIdWithRolesAsync(userId))
            .ReturnsAsync(inactiveUser);
        
        Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);
        
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"User not found or inactive");
    }

    [Fact]
    public async Task Handle_ShouldReturnUserProfile_WhenUserIsValid()
    {
        var userId = Guid.NewGuid();
        var query = new GetMyProfileQuery(userId);
        var validUser = new User
        {
            Id = userId,
            Username = "test",
            FullName = "test",
            Email = "test@user.com",
            Status = "Active",
            UserRoles = new List<UserRole>
            {
                new UserRole
                {
                    Role = new Role { Id = 1, RoleName = "Operator" }
                }
            }
        };
        
        _mockUserRepo.Setup(u => u.GetByIdWithRolesAsync(userId)).ReturnsAsync(validUser);
        
        var result = await _handler.Handle(query, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Id.Should().Be(validUser.Id);
        result.Username.Should().Be(validUser.Username);
        result.Email.Should().Be(validUser.Email);
        result.FullName.Should().Be(validUser.FullName);
        result.Roles.Should().NotBeNull();
        result.Roles.Should().HaveCount(1);
        result.Roles.Should().Contain("Operator");
    } 
}