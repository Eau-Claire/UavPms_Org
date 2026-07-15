using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using Xunit;

namespace UavPms.UnitTests.Features.AIAnalysis;

public class AnalyzeMissionMediaCommandHandlerTests
{
    private readonly Mock<IGenericRepository<Mission>> _missionRepoMock;
    private readonly Mock<IGenericRepository<Asset>> _assetRepoMock;
    private readonly Mock<IGenericRepository<InspectionMedia>> _mediaRepoMock;
    private readonly Mock<IGenericRepository<AIAnalysisRequest>> _aiRequestRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICurrentUserServices> _currentUserMock;
    private readonly Mock<ILogger<AnalyzeMissionMediaCommandHandler>> _loggerMock;

    private readonly AnalyzeMissionMediaCommandHandler _handler;

    public AnalyzeMissionMediaCommandHandlerTests()
    {
        _missionRepoMock = new Mock<IGenericRepository<Mission>>();
        _assetRepoMock = new Mock<IGenericRepository<Asset>>();
        _mediaRepoMock = new Mock<IGenericRepository<InspectionMedia>>();
        _aiRequestRepoMock = new Mock<IGenericRepository<AIAnalysisRequest>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _fileStorageMock = new Mock<IFileStorageService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<AnalyzeMissionMediaCommandHandler>>();

        _handler = new AnalyzeMissionMediaCommandHandler(
            _missionRepoMock.Object,
            _assetRepoMock.Object,
            _mediaRepoMock.Object,
            _aiRequestRepoMock.Object,
            _unitOfWorkMock.Object,
            _fileStorageMock.Object,
            _eventPublisherMock.Object,
            _currentUserMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenMissionDoesNotExist()
    {
        // Arrange
        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(command.MissionId, false))
            .ReturnsAsync((Mission?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{command.MissionId}*");
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenAssetDoesNotExist()
    {
        // Arrange
        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        var mission = new Mission { Id = command.MissionId };

        _missionRepoMock.Setup(r => r.GetByIdAsync(command.MissionId, false))
            .ReturnsAsync(mission);

        _assetRepoMock.Setup(r => r.GetByIdAsync(command.AssetId.Value, false))
            .ReturnsAsync((Asset?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{command.AssetId}*");
    }

    [Fact]
    public async Task Handle_ShouldSaveMediaAndRequestAndPublishEvent_WhenRequestIsValid()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg",
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR",
            Notes = "Test note"
        };

        var mission = new Mission { Id = missionId };
        var asset = new Asset { Id = assetId };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _assetRepoMock.Setup(r => r.GetByIdAsync(assetId, false))
            .ReturnsAsync(asset);

        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("http://storage/test.jpg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileUrl.Should().Be("http://storage/test.jpg");
        result.MediaType.Should().Be("Image");
        result.AnalysisType.Should().Be(AnalysisType.DefectDetection);
        result.Status.Should().Be(AIAnalysisStatus.Pending);

        _mediaRepoMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
             m.MissionId == missionId &&
             m.AssetId == assetId &&
             m.FileUrl == "http://storage/test.jpg" &&
             m.AiSource == "RF-DETR"
         )), Times.Once);

        _aiRequestRepoMock.Verify(r => r.AddAsync(It.Is<AIAnalysisRequest>(req =>
             req.UploadedBy == userId &&
             req.FileUrl == "http://storage/test.jpg" &&
             req.AnalysisType == AnalysisType.DefectDetection &&
             req.Status == AIAnalysisStatus.Pending
         )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(e => e.PublishAsync(It.Is<AIAnalysisRequestedEvent>(evt =>
             evt.FileUrl == "http://storage/test.jpg" &&
             evt.MissionId == missionId &&
             evt.AssetId == assetId &&
             evt.PreferredModel == "RF-DETR" &&
             evt.AnalysisType == "DefectDetection"
         )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSaveMediaAndRequestAndPublishEvent_WhenAssetIdIsNull()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            AssetId = null,
            FileStream = new MemoryStream(),
            FileName = "test.jpg",
            ContentType = "image/jpeg",
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR",
            Notes = "Test note"
        };

        var mission = new Mission { Id = missionId };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("http://storage/test.jpg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileUrl.Should().Be("http://storage/test.jpg");
        result.MediaType.Should().Be("Image");

        _mediaRepoMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
             m.MissionId == missionId &&
             m.AssetId == null &&
             m.FileUrl == "http://storage/test.jpg" &&
             m.AiSource == "RF-DETR"
         )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
