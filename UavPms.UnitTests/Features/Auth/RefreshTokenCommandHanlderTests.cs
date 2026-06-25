using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using UavPms.Application.Features.Auth.Commands.RefreshToken;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using RefreshTokenEntity = UavPms.Core.Entities.RefreshToken;

namespace UavPms.UnitTests.Features.Auth;

public class RefreshTokenCommandHanlderTests
{
    private readonly Mock<IGenericRepository<RefreshTokenEntity>> _refreshTokenRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHanlderTests()
    {
        _refreshTokenRepositoryMock = new Mock<IGenericRepository<RefreshTokenEntity>>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _jwtProviderMock = new Mock<IJwtProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _userRepositoryMock.Object,
            _jwtProviderMock.Object,
            _unitOfWorkMock.Object
        );
    }

    #region Helper

    private static User CreateActivateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FullName = "User",
            Status = "Active",
            Username = "Test user",
            UserRoles = new List<UserRole>
            {
                new UserRole
                {
                    Role = new Role { Id = 1, RoleName = "Admin" }
                }
            }
        };
    }

    private static RefreshTokenEntity CreateRefreshToken(Guid userId)
    {
        return new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "token-hashed",
            ExpiresAt = DateTime.Now.AddDays(5),
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
        };
    }

    #endregion
    
    // Test 1: Refresh TOken không tồn tại -> UnauthorizedAccessException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenTokenNotFound()
    {
        var command = new RefreshTokenCommand("invalid-token", "UserAgent");
        _refreshTokenRepositoryMock.Setup(r =>
            r.FindAsync(It.IsAny<Expression<Func<RefreshTokenEntity, bool>>>(), true))
            .ReturnsAsync(new List<RefreshTokenEntity>()); // Không tìm thấy
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid or expired refresh token");
    }
    
    // Tset: 2: Refresh Token đã hết hạn => UnauthorizedAccessToken
    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenTokenExpired()
    {
        // arrange
        var command = new RefreshTokenCommand("expired-token", "UserAgent");
        var expiredToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "token-hashed",
            ExpiresAt = DateTime.Now.AddDays(-1),
            RevokedAt = null,
        };
        
        _refreshTokenRepositoryMock.Setup(r =>
            r.FindAsync(It.IsAny<Expression<Func<RefreshTokenEntity, bool>>>(), true))
            .ReturnsAsync(new List<RefreshTokenEntity> { expiredToken });
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid or expired refresh token");
    }
    
    // Test 3: User không tồn tại hoặc Inactive -> UnauthorizedException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotFoundOrInactive()
    {
        // arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("valid-token", "UserAgent");
        var token = CreateRefreshToken(userId);
        
        _refreshTokenRepositoryMock.Setup(r =>
            r.FindAsync(It.IsAny<Expression<Func<RefreshTokenEntity, bool>>>(), true))
            .ReturnsAsync(new List<RefreshTokenEntity>{ token });
        _userRepositoryMock.Setup(r =>
            r.GetByIdWithRolesAsync(userId))
            .ReturnsAsync((User?)null); // User does not exist
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User not found or inactive");
    }
    
    // Test 4: Refresh thành công -> revoke old token, trả new token pair
    [Fact]
    public async Task Handle_ShouldRevokeOldAndReturnNewTokens_WhenValid()
    {
        var user = CreateActivateUser();
        var command = new RefreshTokenCommand("valid-token", "UserAgent");
        var existingToken =  CreateRefreshToken(user.Id);
        
        _refreshTokenRepositoryMock.Setup(r => 
            r.FindAsync(It.IsAny<Expression<Func<RefreshTokenEntity, bool>>>(), true))
            .ReturnsAsync(new List<RefreshTokenEntity> { existingToken });
        
        _userRepositoryMock.Setup(r => 
            r.GetByIdWithRolesAsync(user.Id))
            .ReturnsAsync(user);
        
        _jwtProviderMock.Setup(j =>
            j.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");
        
        _jwtProviderMock.Setup(j => 
            j.GenerateRefreshToken())
            .Returns("new-refresh-token");

        _unitOfWorkMock.Setup(u =>
                u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // act
        var result =  await _handler.Handle(command, CancellationToken.None);
        
        // assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
        result.User.Should().NotBeNull();
        result.User!.Roles.Should().Contain("Admin");
        
        // verify new refresh token 
        _refreshTokenRepositoryMock.Verify( r=>
            r.AddAsync(It.Is<RefreshTokenEntity>(t =>
                t.UserId == user.Id && 
                t.DeviceInfo == "UserAgent")), Times.Once);
        _unitOfWorkMock.Verify(u=>u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    // Test 5: Refresh token đã bị revoke -> UnauthorizedAccessException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenTokenIsRevoked()
    {
        var command = new  RefreshTokenCommand("revoked-token", "UserAgent");
         
        _refreshTokenRepositoryMock.Setup(r =>
            r.FindAsync(It.IsAny<Expression<Func<RefreshTokenEntity, bool>>>(), true))
            .ReturnsAsync(new List<RefreshTokenEntity>());
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid or expired refresh token");
    }
}