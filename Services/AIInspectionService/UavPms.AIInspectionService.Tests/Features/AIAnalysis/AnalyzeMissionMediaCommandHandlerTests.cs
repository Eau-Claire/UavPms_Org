using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class AnalyzeMissionMediaCommandHandlerTests
{
    private readonly Mock<IGenericRepository<Mission>> _missionRepoMock;
    private readonly Mock<IGenericRepository<InspectionMedia>> _mediaRepoMock;
    private readonly Mock<IGenericRepository<AIAnalysisRequest>> _aiRequestRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IRealtimeNotificationService> _realtimeNotificationServiceMock;
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
        _realtimeNotificationServiceMock = new Mock<IRealtimeNotificationService>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _loggerMock = new Mock<ILogger<AnalyzeMissionMediaCommandHandler>>();

        _handler = new AnalyzeMissionMediaCommandHandler(
            _missionRepoMock.Object,
            _mediaRepoMock.Object,
            _aiRequestRepoMock.Object,
            _unitOfWorkMock.Object,
            _fileStorageMock.Object,
            _eventPublisherMock.Object,
            _realtimeNotificationServiceMock.Object,
            _currentUserMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenMissionDoesNotExist()
    {
        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = Guid.NewGuid(),
            Files = new List<FileDataDto> { CreateFile("test.jpg", "image/jpeg") }
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(command.MissionId, false))
            .ReturnsAsync((Mission?)null);

        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{command.MissionId}*");
    }

    [Fact]
    public async Task Handle_ShouldCreateMediaRequestsAndEvents_ForMixedImageVideoBatch()
    {
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var addedMedia = new List<InspectionMedia>();
        var addedRequests = new List<AIAnalysisRequest>();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            Files = new List<FileDataDto>
            {
                CreateFile("pole.jpg", "image/jpeg"),
                CreateFile("flight.mp4", "video/mp4")
            },
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR",
            Notes = "day la 1 cay cot dien o Ha NOi"
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId });
        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string fileName) => $"http://storage/{fileName}");
        _mediaRepoMock.Setup(r => r.AddAsync(It.IsAny<InspectionMedia>()))
            .Callback<InspectionMedia>(addedMedia.Add)
            .ReturnsAsync((InspectionMedia media) => media);
        _aiRequestRepoMock.Setup(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()))
            .Callback<AIAnalysisRequest>(addedRequests.Add)
            .ReturnsAsync((AIAnalysisRequest request) => request);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.BatchId.Should().NotBeEmpty();
        result.TotalFiles.Should().Be(2);
        result.AcceptedFiles.Should().Be(2);
        result.RejectedFiles.Should().Be(0);
        result.RequestIds.Should().HaveCount(2);

        addedMedia.Should().HaveCount(2);
        addedMedia.Should().Contain(m => m.MissionId == missionId && m.AssetId == null && m.MediaType == "Image" && m.FileUrl == "http://storage/pole.jpg");
        addedMedia.Should().Contain(m => m.MissionId == missionId && m.AssetId == null && m.MediaType == "Video" && m.FileUrl == "http://storage/flight.mp4");
        addedMedia.Should().OnlyContain(m => m.AiSource == "RF-DETR");

        addedRequests.Should().HaveCount(2);
        addedRequests.Should().OnlyContain(r =>
            r.BatchId == result.BatchId &&
            r.UploadedBy == userId &&
            r.MediaId.HasValue &&
            addedMedia.Select(m => m.Id).Contains(r.MediaId.Value) &&
            r.MissionId == missionId &&
            r.AnalysisType == AnalysisType.DefectDetection &&
            r.Status == AIAnalysisStatus.Pending &&
            r.Notes == command.Notes);
        result.RequestIds.Should().BeEquivalentTo(addedRequests.Select(r => r.Id));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.Is<AIAnalysisRequestedEvent>(evt =>
             evt.MissionId == missionId &&
             evt.MediaId.HasValue &&
             addedMedia.Select(m => m.Id).Contains(evt.MediaId.Value) &&
             evt.AssetId == null &&
             evt.PreferredModel == "RF-DETR" &&
             evt.AnalysisType == "DefectDetection")), Times.Exactly(2));
        _realtimeNotificationServiceMock.Verify(s => s.SendAiAnalysisStatusToUserAsync(
            userId,
            It.Is<AIAnalysisStatusChangedEvent>(evt =>
                evt.BatchId == result.BatchId &&
                evt.MissionId == missionId &&
                evt.Status == "Pending" &&
                result.RequestIds.Contains(evt.RequestId)),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldRejectUnsupportedFilesAndContinueAcceptedFiles()
    {
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            Files = new List<FileDataDto>
            {
                CreateFile("notes.txt", "text/plain"),
                CreateFile("pole.png", "image/png")
            },
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "SERVER"
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId });
        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string fileName) => $"http://storage/{fileName}");
        _mediaRepoMock.Setup(r => r.AddAsync(It.IsAny<InspectionMedia>()))
            .ReturnsAsync((InspectionMedia media) => media);
        _aiRequestRepoMock.Setup(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()))
            .ReturnsAsync((AIAnalysisRequest request) => request);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.TotalFiles.Should().Be(2);
        result.AcceptedFiles.Should().Be(1);
        result.RejectedFiles.Should().Be(1);
        result.RequestIds.Should().HaveCount(1);

        _fileStorageMock.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), "pole.png"), Times.Once);
        _mediaRepoMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m => m.FileUrl == "http://storage/pole.png")), Times.Once);
        _aiRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<AIAnalysisRequestedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRejectFailedFileAndContinueRemainingFiles()
    {
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AnalyzeMissionMediaCommand
        {
            MissionId = missionId,
            Files = new List<FileDataDto>
            {
                CreateFile("broken.jpg", "image/jpeg"),
                CreateFile("ok.webm", "video/webm")
            }
        };

        _missionRepoMock.Setup(r => r.GetByIdAsync(missionId, false))
            .ReturnsAsync(new Mission { Id = missionId });
        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), "broken.jpg"))
            .ThrowsAsync(new IOException("storage failed"));
        _fileStorageMock.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), "ok.webm"))
            .ReturnsAsync("http://storage/ok.webm");
        _mediaRepoMock.Setup(r => r.AddAsync(It.IsAny<InspectionMedia>()))
            .ReturnsAsync((InspectionMedia media) => media);
        _aiRequestRepoMock.Setup(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()))
            .ReturnsAsync((AIAnalysisRequest request) => request);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.TotalFiles.Should().Be(2);
        result.AcceptedFiles.Should().Be(1);
        result.RejectedFiles.Should().Be(1);
        result.RequestIds.Should().HaveCount(1);

        _mediaRepoMock.Verify(r => r.AddAsync(It.Is<InspectionMedia>(m => m.FileUrl == "http://storage/ok.webm")), Times.Once);
        _aiRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<AIAnalysisRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<AIAnalysisRequestedEvent>()), Times.Once);
    }

    private static FileDataDto CreateFile(string fileName, string contentType)
    {
        return new FileDataDto
        {
            Stream = new MemoryStream(new byte[] { 1, 2, 3 }),
            FileName = fileName,
            ContentType = contentType
        };
    }
}
