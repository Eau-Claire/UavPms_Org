using FluentAssertions;
using Moq;
using System.Linq.Expressions;
using UavPms.Application.Features.Users.Commands.UpdateUser;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
namespace UavPms.UnitTests.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IGenericRepository<Role>> _roleRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _roleRepositoryMock = new Mock<IGenericRepository<Role>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateUserCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenUserDoesNotExist()
    {
        var userId =  Guid.NewGuid();
        var command = new UpdateUserCommand(userId, "email@test.com", "Name", "123", "Active", new List<string>());
        _userRepositoryMock.Setup(r =>
                r.GetByIdWithRolesAsync(userId))
            .ReturnsAsync((User?)null);
        
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
    
    [Fact]
    public async Task Handle_ShouldUpdateUser_WhenDataIsValid()
    {
        var userId = Guid.NewGuid();
        var existingUser = new User {Id = userId, Email = "old@email.com", UserRoles = new List<UserRole>() };
        var command = new UpdateUserCommand(userId, "existingEmail", "Name", "123", "Active", new List<string>{ "Manager" });
        
        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(userId)).ReturnsAsync(existingUser);
        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
        _roleRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Role, bool>>>(), false))
            .ReturnsAsync(new List<Role>{ new Role {Id = 2, RoleName = "Manager" } });
        
        var result = await  _handler.Handle(command, CancellationToken.None);
        
        result.Should().BeTrue();
        existingUser.Email.Should().Be("existingEmail");
        _userRepositoryMock.Verify(r => r.UpdateAsync(existingUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
}