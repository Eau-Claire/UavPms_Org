using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public sealed class AiCallbackLifecycleTests
{
    [Fact]
    public async Task Request_ShouldTransitionPendingProcessingCompleted_WithZeroDetections_AndIgnoreDuplicate()
    {
        var fixture = new Fixture();
        (await fixture.Handle("Processing")).Status.Should().Be("Processing");
        fixture.Request.Status.Should().Be(AIAnalysisStatus.Processing);
        fixture.Request.StartedAt.Should().NotBeNull();

        var completed = await fixture.Handle("Completed");
        completed.SavedDetections.Should().Be(0);
        fixture.Request.Status.Should().Be(AIAnalysisStatus.Completed);
        fixture.Request.CompletedAt.Should().NotBeNull();

        await fixture.Handle("Completed");
        fixture.Anomalies.Verify(x => x.AddAsync(It.IsAny<DetectedAnomaly>()), Times.Never);
    }

    [Fact]
    public async Task Request_ShouldTransitionProcessingToFailed()
    {
        var fixture = new Fixture();
        await fixture.Handle("Processing");
        await fixture.Handle("Failed");
        fixture.Request.Status.Should().Be(AIAnalysisStatus.Failed);
        fixture.Request.ErrorMessage.Should().Contain("AI_PROCESSING_FAILED");
        fixture.Request.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Callback_ShouldRejectInconsistentMediaId()
    {
        var fixture = new Fixture();
        var act = () => fixture.Handle("Processing", mediaId: Guid.NewGuid());
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*identifiers*");
    }

    private sealed class Fixture
    {
        public readonly Guid MediaId = Guid.NewGuid();
        public readonly Guid MissionId = Guid.NewGuid();
        public readonly Guid TowerId = Guid.NewGuid();
        public readonly AIAnalysisRun Request;
        public readonly Mock<IAnomalyRepository> Anomalies = new();
        private readonly ProcessAiAnalysisResultCommandHandler _handler;

        public Fixture()
        {
            Request = new AIAnalysisRun
            {
                Id = Guid.NewGuid(), MediaId = MediaId, RequestedBy = Guid.NewGuid(),
                ExecutionLocation = AIAnalysisRun.Cloud, Status = AIAnalysisStatus.Pending
            };
            var requests = new Mock<IGenericRepository<AIAnalysisRun>>();
            requests.Setup(x => x.GetByIdAsync(Request.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(Request);
            var media = new Mock<IInspectionMediaRepository>();
            media.Setup(x => x.GetByIdWithDetailsAsync(MediaId)).ReturnsAsync(new InspectionMedia
            {
                Id = MediaId, MissionId = MissionId, TowerId = TowerId, FileUrl = "media.jpg", MediaType = "Image"
            });
            _handler = new ProcessAiAnalysisResultCommandHandler(requests.Object, media.Object,
                Mock.Of<IGenericRepository<DefectCategory>>(), Anomalies.Object, Mock.Of<IUnitOfWork>(),
                Mock.Of<IInspectionEvaluationClient>(), Mock.Of<IEventPublisher>(),
                Mock.Of<IGenericRepository<OutboxMessage>>(),
                Mock.Of<ILogger<ProcessAiAnalysisResultCommandHandler>>());
        }

        public Task<AiAnalysisCallbackResponseDto> Handle(string status, Guid? mediaId = null) => _handler.Handle(new()
        {
            RequestId = Request.Id, MediaId = mediaId ?? MediaId, MissionId = MissionId,
            Status = status, ModelName = status == "Completed" ? "server" : null,
            Detections = status == "Completed" ? [] : null,
            ErrorCode = status == "Failed" ? "AI_PROCESSING_FAILED" : null,
            ErrorMessage = status == "Failed" ? "worker failure" : null,
            CompletedAt = DateTime.UtcNow
        }, CancellationToken.None);
    }
}
