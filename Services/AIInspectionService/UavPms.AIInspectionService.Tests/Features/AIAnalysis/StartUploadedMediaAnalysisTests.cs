using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.StartUploadedMediaAnalysis;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public sealed class StartUploadedMediaAnalysisTests
{
    [Fact]
    public async Task UploadEvent_ShouldCreateExactlyOneRequest_AndReplayShouldBeIdempotent()
    {
        var mediaId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var upload = new InspectionMediaUploadedEvent
        {
            EventId = Guid.NewGuid(), MediaId = mediaId, MissionId = missionId, TowerId = towerId,
            UploadedBy = uploaderId, FileUrl = "https://storage/media.jpg", MediaType = "Image"
        };
        var mediaRepo = new Mock<IGenericRepository<InspectionMedia>>();
        var requestRepo = new Mock<IGenericRepository<AIAnalysisRun>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var publisher = new Mock<IEventPublisher>();
        AIAnalysisRun? persisted = null;
        mediaRepo.Setup(x => x.GetByIdAsync(mediaId, false, It.IsAny<CancellationToken>())).ReturnsAsync(
            new InspectionMedia { Id = mediaId, MissionId = missionId, TowerId = towerId, UploadedBy = uploaderId,
                FileUrl = upload.FileUrl, MediaType = "Image" });
        requestRepo.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AIAnalysisRun, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(() => persisted == null ? [] : [persisted]);
        requestRepo.Setup(x => x.AddAsync(It.IsAny<AIAnalysisRun>())).ReturnsAsync((AIAnalysisRun value) => persisted = value);

        var handler = new StartUploadedMediaAnalysisCommandHandler(mediaRepo.Object, requestRepo.Object,
            unitOfWork.Object, publisher.Object, Mock.Of<ILogger<StartUploadedMediaAnalysisCommandHandler>>());

        var first = await handler.Handle(new StartUploadedMediaAnalysisCommand(upload), CancellationToken.None);
        var replay = await handler.Handle(new StartUploadedMediaAnalysisCommand(upload), CancellationToken.None);

        replay.Should().Be(first);
        requestRepo.Verify(x => x.AddAsync(It.IsAny<AIAnalysisRun>()), Times.Once);
        publisher.Verify(x => x.PublishAsync(It.Is<AIAnalysisRequestedEvent>(e =>
            e.RequestId == first && e.FileUrl == upload.FileUrl && e.MediaType == "Image" &&
            e.MissionId == missionId && e.AssetId == towerId && e.UploadedBy == uploaderId)), Times.Exactly(2));
        persisted!.ExecutionLocation.Should().Be(AIAnalysisRun.Cloud);
        persisted.RequestedBy.Should().Be(uploaderId);
        persisted.SourceEventId.Should().Be(upload.EventId);
    }

    [Fact]
    public async Task UploadEvent_WithoutTower_ShouldStillCreateRun()
    {
        var mediaId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var upload = new InspectionMediaUploadedEvent
        {
            EventId = Guid.NewGuid(), MediaId = mediaId, MissionId = missionId, TowerId = null,
            UploadedBy = uploaderId, FileUrl = "https://storage/corridor.mp4", MediaType = "Video"
        };
        var mediaRepo = new Mock<IGenericRepository<InspectionMedia>>();
        var requestRepo = new Mock<IGenericRepository<AIAnalysisRun>>();
        var publisher = new Mock<IEventPublisher>();
        mediaRepo.Setup(x => x.GetByIdAsync(mediaId, false, It.IsAny<CancellationToken>())).ReturnsAsync(
            new InspectionMedia { Id = mediaId, MissionId = missionId, UploadedBy = uploaderId,
                FileUrl = upload.FileUrl, MediaType = "Video" });
        requestRepo.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AIAnalysisRun, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<AIAnalysisRun>());
        requestRepo.Setup(x => x.AddAsync(It.IsAny<AIAnalysisRun>())).ReturnsAsync((AIAnalysisRun value) => value);

        var handler = new StartUploadedMediaAnalysisCommandHandler(mediaRepo.Object, requestRepo.Object,
            Mock.Of<IUnitOfWork>(), publisher.Object, Mock.Of<ILogger<StartUploadedMediaAnalysisCommandHandler>>());

        await handler.Handle(new StartUploadedMediaAnalysisCommand(upload), CancellationToken.None);

        requestRepo.Verify(x => x.AddAsync(It.IsAny<AIAnalysisRun>()), Times.Once);
        publisher.Verify(x => x.PublishAsync(It.Is<AIAnalysisRequestedEvent>(e => e.AssetId == null && e.MediaType == "Video")), Times.Once);
    }
}
