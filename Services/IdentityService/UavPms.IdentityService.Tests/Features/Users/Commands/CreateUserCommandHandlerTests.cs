using FluentAssertions;
using Moq;
using System.Linq.Expressions;
using UavPms.IdentityService.Application.Features.Users.Commands.CreateUser;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
namespace UavPms.IdentityService.Tests.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IGenericRepository<Role>> _roleRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _roleRepositoryMock = new Mock<IGenericRepository<Role>>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateUserCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenEmailAlreadyInUse()
    {
        var command = new CreateUserCommand("existingemail@gmail.com", "pass", "Name", "123",
            new List<string>());
        _userRepositoryMock.Setup(u =>
            u.GetByEmailWithRolesAsync("existingemail@gmail.com"))
            .ReturnsAsync(new User());
        
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Email already exists.");
    }

    [Fact]
    public async Task Handle_ShouldCreateUser_WhenDataIsValid()
    {
        var command = new CreateUserCommand(" ExistingEmail@GMAIL.COM ", "pass", "Name", "123",
            new List<string>());
        _userRepositoryMock.Setup(u =>
            u.GetByEmailWithRolesAsync("existingemail@gmail.com"))
            .ReturnsAsync((User?)null);
        
        var mockRole = new Role {Id = 1, RoleName = "Manager"};
        _roleRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Role, bool>>>(), false))
            .ReturnsAsync(new List<Role>{ mockRole});
        
        _passwordHasherMock.Setup(p =>
            p.Hash(command.Password)).Returns("hashedpass");
        
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        _userRepositoryMock.Verify(r =>
            r.AddAsync(It.Is<User>(u => u.Email == "existingemail@gmail.com")), Times.Once);
        _unitOfWorkMock.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
