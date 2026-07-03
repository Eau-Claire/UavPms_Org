using FluentAssertions;
using Moq;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Application.Features.Notifications.Commands.DeleteNotification;
using UavPms.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.UnitTests.Features.Notifications;

public class NotificationCommandHandlersTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public NotificationCommandHandlersTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateNotificationCommand tests

    [Fact]
    public async Task Handle_CreateNotification_ShouldSaveAndReturnDto()
    {
        // arrange
        var command = new CreateNotificationCommand(
            UserId: Guid.NewGuid(),
            Type: "TestType",
            ReferenceType: "TestRef",
            ReferenceId: Guid.NewGuid(),
            Title: "Test Title",
            Body: "Test Body"
        );

        var handler = new CreateNotificationCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object
        );

        // act
        var result = await handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(command.UserId);
        result.Type.Should().Be(command.Type);
        result.ReferenceType.Should().Be(command.ReferenceType);
        result.ReferenceId.Should().Be(command.ReferenceId);
        result.Title.Should().Be(command.Title);
        result.Body.Should().Be(command.Body);
        result.IsRead.Should().BeFalse();

        _notificationRepositoryMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == command.UserId &&
            n.Type == command.Type &&
            n.Title == command.Title &&
            n.Body == command.Body
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region MarkNotificationAsReadCommand tests

    [Fact]
    public async Task Handle_MarkAsRead_ShouldCallRepository_WhenNotificationExists()
    {
        // arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification { Id = notificationId };
        var command = new MarkNotificationAsReadCommand(notificationId);

        _notificationRepositoryMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<bool>()))
            .ReturnsAsync(notification);

        var handler = new MarkNotificationAsReadCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object
        );

        // act
        await handler.Handle(command, CancellationToken.None);

        // assert
        _notificationRepositoryMock.Verify(r => r.MarkAsReadAsync(notificationId), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MarkAsRead_ShouldThrowNotFoundException_WhenNotificationDoesNotExist()
    {
        // arrange
        var notificationId = Guid.NewGuid();
        var command = new MarkNotificationAsReadCommand(notificationId);

        _notificationRepositoryMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<bool>()))
            .ReturnsAsync((Notification?)null);

        var handler = new MarkNotificationAsReadCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object
        );

        // act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<NotFoundException>();
        _notificationRepositoryMock.Verify(r => r.MarkAsReadAsync(It.IsAny<Guid>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteNotificationCommand tests

    [Fact]
    public async Task Handle_Delete_ShouldCallRepository_WhenNotificationExists()
    {
        // arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification { Id = notificationId };
        var command = new DeleteNotificationCommand(notificationId);

        _notificationRepositoryMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<bool>()))
            .ReturnsAsync(notification);

        var handler = new DeleteNotificationCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object
        );

        // act
        await handler.Handle(command, CancellationToken.None);

        // assert
        _notificationRepositoryMock.Verify(r => r.DeleteAsync(notification), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Delete_ShouldThrowNotFoundException_WhenNotificationDoesNotExist()
    {
        // arrange
        var notificationId = Guid.NewGuid();
        var command = new DeleteNotificationCommand(notificationId);

        _notificationRepositoryMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<bool>()))
            .ReturnsAsync((Notification?)null);

        var handler = new DeleteNotificationCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object
        );

        // act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<NotFoundException>();
        _notificationRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Notification>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
