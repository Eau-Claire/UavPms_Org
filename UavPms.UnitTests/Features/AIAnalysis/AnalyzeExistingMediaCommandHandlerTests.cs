using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.UnitTests.Features.AIAnalysis;

public class AnalyzeExistingMediaCommandHandlerTests
{
    private readonly Mock<IGenericRepository<Mission>> _missionRepoMock;
    private readonly Mock<IGenericRepository<InspectionMedia>> _mediaRepoMock;
    private readonly Mock<IGenericRepository<AIAnalysisRequest>> _aiRequestRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICurrentUserServices> _currentUserMock;
    private readonly Mock<ILogger<AnalyzeExistingMediaCommandHandler>> _loggerMock;

    private readonly AnalyzeExistingMediaCommandHandler _handler;

    public AnalyzeExistingMediaCommandHandlerTests()
    {
        _missionRepoMock = new Mock<IGenericRepository<Mission>>();
        _mediaRepoMock = new Mock<IGenericRepository<InspectionMedia>>();
        _aiRequestRepoMock = new Mock<IGenericRepository<AIAnalysisRequest>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<AnalyzeExistingMediaCommandHandler>>();

        _handler = new AnalyzeExistingMediaCommandHandler(
            _missionRepoMock.Object,
            _mediaRepoMock.Object,
            _aiRequestRepoMock.Object,
            _unitOfWorkMock.Object,
            _eventPublisherMock.Object,
            _currentUserMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenMissionDoesNotExist()
    {
        // Arrange
        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = Guid.NewGuid(),
            MediaId = Guid.NewGuid()
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
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenMediaDoesNotExist()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = Guid.NewGuid()
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId });

        _mediaRepoMock.Setup(r => r.GetByIdAsync(command.MediaId, false))
            .ReturnsAsync((InspectionMedia?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{command.MediaId}*");
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenMediaDoesNotBelongToMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = Guid.NewGuid()
        };

        var media = new InspectionMedia
        {
            Id = command.MediaId,
            MissionId = Guid.NewGuid() // Different mission
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId });

        _mediaRepoMock.Setup(r => r.GetByIdAsync(command.MediaId, false))
            .ReturnsAsync(media);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*does not belong*");
    }

    [Fact]
    public async Task Handle_ShouldSaveRequestAndPublishEvent_WhenRequestIsValid()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = Guid.NewGuid(),
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "YOLO11",
            Notes = "Analyze existing media note"
        };

        var mission = new Mission { Id = missionId };
        var media = new InspectionMedia
        {
            Id = command.MediaId,
            MissionId = missionId,
            AssetId = assetId,
            FileUrl = "http://storage/existing.jpg",
            MediaType = "Image"
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(mission);

        _mediaRepoMock.Setup(r => r.GetByIdAsync(command.MediaId, false))
            .ReturnsAsync(media);

        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileUrl.Should().Be("http://storage/existing.jpg");
        result.MediaType.Should().Be("Image");
        result.AnalysisType.Should().Be(AnalysisType.DefectDetection);
        result.Status.Should().Be(AIAnalysisStatus.Pending);

        _aiRequestRepoMock.Verify(r => r.AddAsync(It.Is<AIAnalysisRequest>(req =>
            req.UploadedBy == userId &&
            req.MediaId == media.Id &&
            req.MissionId == missionId &&
            req.FileUrl == "http://storage/existing.jpg" &&
            req.AnalysisType == AnalysisType.DefectDetection &&
            req.Status == AIAnalysisStatus.Pending
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(e => e.PublishAsync(It.Is<AIAnalysisRequestedEvent>(evt =>
            evt.FileUrl == "http://storage/existing.jpg" &&
            evt.MediaId == media.Id &&
            evt.MissionId == missionId &&
            evt.AssetId == assetId &&
            evt.PreferredModel == "YOLO11" &&
            evt.AnalysisType == "DefectDetection"
        )), Times.Once);
    }
}
