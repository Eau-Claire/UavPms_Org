using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.Application.Features.Inspections.Commands.UploadImage;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using Xunit;

namespace UavPms.UnitTests.Features.Inspections;

public class UploadInspectionImageCommandHandlerTests
{
    private readonly Mock<IGenericRepository<Mission>> _missionRepositoryMock;
    private readonly Mock<IGenericRepository<Asset>> _assetRepositoryMock;
    private readonly Mock<IGenericRepository<InspectionMedia>> _mediaRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<ILogger<UploadInspectionImageCommandHandler>> _loggerMock;
    private readonly UploadInspectionImageCommandHandler _handler;

    public UploadInspectionImageCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IGenericRepository<Mission>>();
        _assetRepositoryMock = new Mock<IGenericRepository<Asset>>();
        _mediaRepositoryMock = new Mock<IGenericRepository<InspectionMedia>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<UploadInspectionImageCommandHandler>>();

        _handler = new UploadInspectionImageCommandHandler(
            _missionRepositoryMock.Object,
            _assetRepositoryMock.Object,
            _mediaRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _fileStorageServiceMock.Object,
            _eventPublisherMock.Object,
            _currentUserServicesMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenMissionDoesNotExist()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = Guid.NewGuid(),
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync((Mission?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Mission with ID '{missionId}' was not found.");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<ImageUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenAssetDoesNotExist()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        var mission = new Mission { Id = missionId };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(assetId, false))
            .ReturnsAsync((Asset?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Asset with ID '{assetId}' was not found.");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<ImageUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAssignedInspector()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid(); // different from inspectorId

        var mission = new Mission
        {
            Id = missionId,
            InspectorId = inspectorId
        };

        var asset = new Asset { Id = assetId };

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(assetId, false))
            .ReturnsAsync(asset);

        _currentUserServicesMock.Setup(s => s.UserId).Returns(currentUserId);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not assigned to this mission.");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<ImageUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSaveImageAndSaveToDbAndPublishEvent_WhenValidRequest()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var fileUrl = "/images/unique_test.jpg";
        var fileName = "test.jpg";
        var contentType = "image/jpeg";

        var mission = new Mission
        {
            Id = missionId,
            InspectorId = inspectorId
        };

        var asset = new Asset { Id = assetId };
        var capturedAt = DateTime.UtcNow;

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            CapturedAt = capturedAt,
            FileStream = new MemoryStream(),
            FileName = fileName,
            ContentType = contentType
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(assetId, false))
            .ReturnsAsync(asset);

        _currentUserServicesMock.Setup(s => s.UserId).Returns(inspectorId);

        _fileStorageServiceMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(fileUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.MissionId.Should().Be(missionId);
        result.FileUrl.Should().Be(fileUrl);
        result.MediaType.Should().Be("Image");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), fileName), Times.Once);
        
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
             m.MissionId == missionId &&
             m.AssetId == assetId &&
             m.FileUrl == fileUrl &&
             m.MediaType == "Image" &&
             m.ValidationStatus == "Pending" &&
             m.CapturedAt == capturedAt &&
             m.CreatedBy == inspectorId
         )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(p => p.PublishAsync(It.Is<ImageUploadedEvent>(e =>
            e.MissionId == missionId &&
            e.FileUrl == fileUrl &&
            e.MediaType == "Image" &&
            e.UploadedBy == inspectorId
        )), Times.Once);
    }
}
