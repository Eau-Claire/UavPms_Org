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
        _mediaRepoMock = new Mock<IGenericRepository<InspectionMedia>>();
        _aiRequestRepoMock = new Mock<IGenericRepository<AIAnalysisRequest>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _fileStorageMock = new Mock<IFileStorageService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<AnalyzeMissionMediaCommandHandler>>();

        _handler = new AnalyzeMissionMediaCommandHandler(
            _missionRepoMock.Object,
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
            Files = new List<FileDataDto>
            {
                new() { Stream = new MemoryStream(), FileName = "test.jpg", ContentType = "image/jpeg" }
            }
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
    public async Task Handle_ShouldSaveMediaAndRequestAndPublishEvent_WhenRequestIsValid()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            Files = new List<FileDataDto>
            {
                new() { Stream = new MemoryStream(new byte[] { 1, 2, 3 }), FileName = "test.jpg", ContentType = "image/jpeg" },
                new() { Stream = new MemoryStream(new byte[] { 4, 5, 6 }), FileName = "test_video.mp4", ContentType = "video/mp4" }
            },
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR",
            Notes = "Test notes"
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
        result.BatchId.Should().NotBeEmpty();
        result.TotalFiles.Should().Be(2);
        result.AcceptedFiles.Should().Be(2);
        result.RejectedFiles.Should().Be(0);
        result.RequestIds.Should().HaveCount(2);

        _mediaRepoMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m =>
             m.MissionId == missionId &&
             m.AssetId == null &&
             m.FileUrl == "http://storage/test.jpg" &&
             m.AiSource == "RF-DETR"
         )), Times.Exactly(2));

        _aiRequestRepoMock.Verify(r => r.AddAsync(It.Is<AIAnalysisRequest>(req =>
             req.UploadedBy == userId &&
             req.FileUrl == "http://storage/test.jpg" &&
             req.AnalysisType == AnalysisType.DefectDetection &&
             req.Status == AIAnalysisStatus.Pending &&
             req.BatchId == result.BatchId
         )), Times.Exactly(2));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(e => e.PublishAsync(It.Is<AIAnalysisRequestedEvent>(evt =>
             evt.FileUrl == "http://storage/test.jpg" &&
             evt.MissionId == missionId &&
             evt.AssetId == null &&
             evt.PreferredModel == "RF-DETR" &&
             evt.AnalysisType == "DefectDetection"
         )), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldRejectFiles_WhenContentTypeIsUnsupported()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            Files = new List<FileDataDto>
            {
                new() { Stream = new MemoryStream(new byte[] { 1 }), FileName = "test.txt", ContentType = "text/plain" },
                new() { Stream = new MemoryStream(new byte[] { 2 }), FileName = "test.jpg", ContentType = "image/jpeg" }
            },
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR",
            Notes = "Test notes"
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
        result.TotalFiles.Should().Be(2);
        result.AcceptedFiles.Should().Be(1);
        result.RejectedFiles.Should().Be(1);
        result.RequestIds.Should().HaveCount(1);

        _mediaRepoMock.Verify(r => r.AddAsync(It.IsAny<InspectionMedia>()), Times.Once);
        _aiRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
