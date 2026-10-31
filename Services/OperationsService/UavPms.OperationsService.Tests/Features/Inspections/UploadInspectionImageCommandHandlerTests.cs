using UavPms.Shared.Contracts.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.Domain.Contracts;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class UploadInspectionImageCommandHandlerTests
{
    private readonly Mock<IGenericRepository<Mission>> _missionRepositoryMock;
    private readonly Mock<IGenericRepository<Tower>> _assetRepositoryMock;
    private readonly Mock<IGenericRepository<InspectionMedia>> _mediaRepositoryMock;
    private readonly Mock<IGenericRepository<MissionTarget>> _missionTargetRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<ILogger<UploadInspectionImageCommandHandler>> _loggerMock;
    private readonly UploadInspectionImageCommandHandler _handler;

    public UploadInspectionImageCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IGenericRepository<Mission>>();
        _assetRepositoryMock = new Mock<IGenericRepository<Tower>>();
        _mediaRepositoryMock = new Mock<IGenericRepository<InspectionMedia>>();
        _missionTargetRepositoryMock = new Mock<IGenericRepository<MissionTarget>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<UploadInspectionImageCommandHandler>>();

        _handler = new UploadInspectionImageCommandHandler(
            _missionRepositoryMock.Object,
            _assetRepositoryMock.Object,
            _mediaRepositoryMock.Object,
            _missionTargetRepositoryMock.Object,
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
            TowerId = Guid.NewGuid(),
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

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<InspectionMediaUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenAssetDoesNotExist()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        var mission = new Mission { Id = missionId, Status = MissionStatus.InProgress };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(towerId, false))
            .ReturnsAsync((Tower?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Tower with ID '{towerId}' was not found.");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<InspectionMediaUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAssignedInspector()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid(); // different from inspectorId

        var mission = new Mission
        {
            Id = missionId,
            Assignments = { new MissionAssignment { UserId = inspectorId, AssignmentRole = "INSPECTOR" } },
            Status = MissionStatus.InProgress
        };

        var tower = new Tower { Id = towerId };

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(towerId, false))
            .ReturnsAsync(tower);

        _currentUserServicesMock.Setup(s => s.UserId).Returns(currentUserId);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not assigned to this mission.");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<InspectionMediaUploadedEvent>()), Times.Never);
    }

    [Theory]
    [InlineData(UserRoles.SystemAdmin, false, true)]
    [InlineData(UserRoles.Manager, true, true)]
    [InlineData(UserRoles.Manager, false, false)]
    [InlineData(UserRoles.Analyst, true, false)]
    public async Task Handle_UnassignedUser_MayUploadOnlyAsAdminOrTheMissionManager(string role, bool isMissionManager, bool allowed)
    {
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            ManagerId = isMissionManager ? currentUserId : Guid.NewGuid(),
            Assignments = { new MissionAssignment { UserId = Guid.NewGuid(), AssignmentRole = "INSPECTOR" } },
            Status = MissionStatus.InProgress
        };
        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false)).ReturnsAsync(mission);
        _assetRepositoryMock.Setup(r => r.GetByIdAsync(towerId, false)).ReturnsAsync(new Tower { Id = towerId });
        _currentUserServicesMock.Setup(s => s.UserId).Returns(currentUserId);
        _currentUserServicesMock.Setup(s => s.Roles).Returns(new[] { role });
        _missionTargetRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MissionTarget, bool>>>(), false))
            .ReturnsAsync(new List<MissionTarget> { new() { MissionId = missionId, TowerId = towerId } });
        _fileStorageServiceMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), "test.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uav-media/test.jpg");

        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        if (allowed)
            await act.Should().NotThrowAsync();
        else
            await act.Should().ThrowAsync<ForbiddenException>().WithMessage("You are not assigned to this mission.");
    }

    [Theory]
    [InlineData(MissionStatus.Draft)]
    [InlineData(MissionStatus.PendingAcceptance)]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenMissionInPreExecutionState(MissionStatus status)
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            Assignments = { new MissionAssignment { UserId = inspectorId, AssignmentRole = "INSPECTOR" } },
            Status = status
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = DateTime.UtcNow,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Cannot upload inspection media*");
    }

    [Fact]
    public async Task Handle_ShouldSaveImageAndSaveToDbAndPublishEvent_WhenValidRequest()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var fileUrl = "/images/unique_test.jpg";
        var fileName = "test.jpg";
        var contentType = "image/jpeg";

        var mission = new Mission
        {
            Id = missionId,
            Assignments = { new MissionAssignment { UserId = inspectorId, AssignmentRole = "INSPECTOR" } },
            Status = MissionStatus.InProgress
        };

        var tower = new Tower { Id = towerId };
        var capturedAt = DateTime.UtcNow;

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = capturedAt,
            FileStream = new MemoryStream(),
            FileName = fileName,
            ContentType = contentType
        };

        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepositoryMock.Setup(r => r.GetByIdAsync(towerId, false))
            .ReturnsAsync(tower);

        _currentUserServicesMock.Setup(s => s.UserId).Returns(inspectorId);

        _missionTargetRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MissionTarget, bool>>>(), false))
            .ReturnsAsync(new List<MissionTarget> { new() { MissionId = missionId, TowerId = towerId } });

        _fileStorageServiceMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.MissionId.Should().Be(missionId);
        result.FileUrl.Should().Be(fileUrl);
        result.MediaType.Should().Be("Image");

        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), fileName, It.IsAny<CancellationToken>()), Times.Once);
        
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
             m.MissionId == missionId &&
             m.TowerId == towerId &&
             m.FileUrl == fileUrl &&
             m.MediaType == "Image" &&
             m.ValidationStatus == "Pending" &&
             m.CapturedAt == capturedAt &&
             m.CreatedBy == inspectorId
         )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(p => p.PublishAsync(It.Is<InspectionMediaUploadedEvent>(e =>
            e.MissionId == missionId &&
            e.TowerId == towerId &&
            e.FileUrl == fileUrl &&
            e.MediaType == "Image" &&
            e.UploadedBy == inspectorId
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRejectAssetOutsideMissionScope()
    {
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId, Status = MissionStatus.InProgress, Assignments = { new MissionAssignment { UserId = inspectorId, AssignmentRole = "INSPECTOR" } } });
        _assetRepositoryMock.Setup(r => r.GetByIdAsync(towerId, false))
            .ReturnsAsync(new Tower { Id = towerId });
        _currentUserServicesMock.SetupGet(x => x.UserId).Returns(inspectorId);
        _missionTargetRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MissionTarget, bool>>>(), false))
            .ReturnsAsync(Array.Empty<MissionTarget>());

        var act = () => _handler.Handle(new UploadInspectionImageCommand
        {
            MissionId = missionId, TowerId = towerId, CapturedAt = DateTime.UtcNow,
            FileName = "test.jpg", ContentType = "image/jpeg", FileStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9])
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UavPms.OperationsService.Application.Common.Exceptions.BusinessRuleException>()
            .WithMessage("*mission inspection scope*");
        _mediaRepositoryMock.Verify(x => x.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
    }

    // Minimal PNG: signature + IHDR (1920x1080) + IEND.
    private static byte[] Png1920x1080() =>
    [
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0x07, 0x80, 0, 0, 0x04, 0x38, 8, 2, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private (Guid MissionId, Guid InspectorId) ArrangeAssignedMissionWithoutTower()
    {
        var missionId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        _missionRepositoryMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId, Status = MissionStatus.InProgress, Assignments = { new MissionAssignment { UserId = inspectorId, AssignmentRole = "INSPECTOR" } } });
        _currentUserServicesMock.SetupGet(x => x.UserId).Returns(inspectorId);
        _fileStorageServiceMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/images/pole.png");
        return (missionId, inspectorId);
    }

    [Fact]
    public async Task Handle_StoresChecksumSizeAndImageDimensions()
    {
        var (missionId, _) = ArrangeAssignedMissionWithoutTower();
        var bytes = Png1920x1080();
        _mediaRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InspectionMedia, bool>>>(), false))
            .ReturnsAsync(Array.Empty<InspectionMedia>());

        var result = await _handler.Handle(new UploadInspectionImageCommand
        {
            MissionId = missionId, CapturedAt = DateTime.UtcNow,
            FileName = "pole.png", ContentType = "image/png", FileStream = new MemoryStream(bytes)
        }, CancellationToken.None);

        var expectedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        result.IsDuplicate.Should().BeFalse();
        result.Checksum.Should().Be(expectedChecksum);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
            m.Checksum == expectedChecksum && m.FileSizeBytes == bytes.Length && m.WidthPx == 1920 && m.HeightPx == 1080
            && m.TowerId == null && m.DurationSeconds == null)), Times.Once);
    }

    [Fact]
    public async Task Handle_SameFileSameMission_ReturnsExistingMediaWithoutStoringAgain()
    {
        var (missionId, _) = ArrangeAssignedMissionWithoutTower();
        var existing = new InspectionMedia { Id = Guid.NewGuid(), MissionId = missionId, FileUrl = "/images/old.png", MediaType = "Image", Checksum = "abc" };
        _mediaRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InspectionMedia, bool>>>(), false))
            .ReturnsAsync(new[] { existing });

        var result = await _handler.Handle(new UploadInspectionImageCommand
        {
            MissionId = missionId, CapturedAt = DateTime.UtcNow,
            FileName = "pole.png", ContentType = "image/png", FileStream = new MemoryStream(Png1920x1080())
        }, CancellationToken.None);

        result.IsDuplicate.Should().BeTrue();
        result.MediaId.Should().Be(existing.Id);
        result.FileUrl.Should().Be("/images/old.png");
        _fileStorageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<InspectionMediaUploadedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameFileOtherMission_ThrowsDuplicateMedia()
    {
        var (missionId, _) = ArrangeAssignedMissionWithoutTower();
        var existing = new InspectionMedia { Id = Guid.NewGuid(), MissionId = Guid.NewGuid(), FileUrl = "/images/old.png", MediaType = "Image" };
        _mediaRepositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InspectionMedia, bool>>>(), false))
            .ReturnsAsync(new[] { existing });

        var act = () => _handler.Handle(new UploadInspectionImageCommand
        {
            MissionId = missionId, CapturedAt = DateTime.UtcNow,
            FileName = "pole.png", ContentType = "image/png", FileStream = new MemoryStream(Png1920x1080())
        }, CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("DUPLICATE_MEDIA");
        _mediaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Never);
    }
}
