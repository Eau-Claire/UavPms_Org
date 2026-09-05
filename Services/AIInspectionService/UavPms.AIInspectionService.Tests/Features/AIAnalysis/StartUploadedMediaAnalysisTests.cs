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
        var assetId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var upload = new InspectionMediaUploadedEvent
        {
            EventId = Guid.NewGuid(), MediaId = mediaId, MissionId = missionId, AssetId = assetId,
            UploadedBy = uploaderId, FileUrl = "https://storage/media.jpg", MediaType = "Image"
        };
        var mediaRepo = new Mock<IGenericRepository<InspectionMedia>>();
        var requestRepo = new Mock<IGenericRepository<AIAnalysisRequest>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var publisher = new Mock<IEventPublisher>();
        AIAnalysisRequest? persisted = null;
        mediaRepo.Setup(x => x.GetByIdAsync(mediaId, false, It.IsAny<CancellationToken>())).ReturnsAsync(
            new InspectionMedia { Id = mediaId, MissionId = missionId, AssetId = assetId, UploadedBy = uploaderId,
                FileUrl = upload.FileUrl, MediaType = "Image" });
        requestRepo.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AIAnalysisRequest, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(() => persisted == null ? [] : [persisted]);
        requestRepo.Setup(x => x.AddAsync(It.IsAny<AIAnalysisRequest>())).ReturnsAsync((AIAnalysisRequest value) => persisted = value);

        var handler = new StartUploadedMediaAnalysisCommandHandler(mediaRepo.Object, requestRepo.Object,
            unitOfWork.Object, publisher.Object, Mock.Of<ILogger<StartUploadedMediaAnalysisCommandHandler>>());

        var first = await handler.Handle(new StartUploadedMediaAnalysisCommand(upload), CancellationToken.None);
        var replay = await handler.Handle(new StartUploadedMediaAnalysisCommand(upload), CancellationToken.None);

        replay.Should().Be(first);
        requestRepo.Verify(x => x.AddAsync(It.IsAny<AIAnalysisRequest>()), Times.Once);
        publisher.Verify(x => x.PublishAsync(It.IsAny<AIAnalysisRequestedEvent>()), Times.Exactly(2));
    }
}
