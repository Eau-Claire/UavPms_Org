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
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

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

    [Fact]
    public async Task Request_ShouldCompleteDirectlyFromPending_WhenBridgeSendsOnlyTerminalState()
    {
        var fixture = new Fixture();
        await fixture.Handle("Completed");
        fixture.Request.Status.Should().Be(AIAnalysisStatus.Completed);
        fixture.Request.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Completed_ShouldMapModelLabelsFromSystemSettings_AndSkipNonDefectOrUnknownLabels()
    {
        var fixture = new Fixture();
        var category = new DefectCategory { Id = 7, CategoryCode = "DB_BIRD_NEST", CategoryName = "Bird nest" };
        fixture.Settings.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new SystemSettingDto(Guid.NewGuid(), "AiLabel:bird_nest", "DB_BIRD_NEST", null, "AI", "string", null),
            new SystemSettingDto(Guid.NewGuid(), "AiLabel:insulator", "NONE", null, "AI", "string", null)
        ]);
        fixture.Categories
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DefectCategory, bool>>>(), false))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<DefectCategory, bool>> predicate, bool _) =>
                new[] { category }.Where(predicate.Compile()).ToList());

        var result = await fixture.Handle("Completed", detections:
        [
            Detection("bird_nest"),
            Detection("insulator"),
            Detection("unmapped_label")
        ]);

        result.SavedDetections.Should().Be(1);
        fixture.Anomalies.Verify(x => x.AddAsync(It.Is<DetectedAnomaly>(a =>
            a.CategoryId == 7 && a.ValidationStatus == DetectedAnomaly.StatusInReview)), Times.Once);
        fixture.Request.Status.Should().Be(AIAnalysisStatus.Completed);
    }

    [Theory]
    [InlineData(DetectedAnomaly.StatusInReview, true)]
    [InlineData(DetectedAnomaly.StatusPending, true)]
    [InlineData(DetectedAnomaly.StatusConfirmed, false)]
    [InlineData(DetectedAnomaly.StatusRejected, false)]
    public void Anomaly_ShouldOnlyBeReviewableWhileInReview(string status, bool expected)
    {
        new DetectedAnomaly { ValidationStatus = status }.IsAwaitingReview.Should().Be(expected);
    }

    private static DetectionDto Detection(string label) => new()
    {
        Id = Guid.NewGuid().ToString(), CategoryCode = label, ClassName = label, Confidence = 0.9,
        BoundingBox = new BoundingBoxDto { X = 0.1, Y = 0.1, Width = 0.2, Height = 0.2 }
    };

    private sealed class Fixture
    {
        public readonly Guid MediaId = Guid.NewGuid();
        public readonly Guid MissionId = Guid.NewGuid();
        public readonly Guid TowerId = Guid.NewGuid();
        public readonly AIAnalysisRun Request;
        public readonly Mock<IAnomalyRepository> Anomalies = new();
        public readonly Mock<ISystemSettingService> Settings = new();
        public readonly Mock<IGenericRepository<DefectCategory>> Categories = new();
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
            Settings.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SystemSettingDto>());
            _handler = new ProcessAiAnalysisResultCommandHandler(requests.Object, media.Object,
                Categories.Object, Anomalies.Object, Mock.Of<IUnitOfWork>(),
                Mock.Of<IInspectionEvaluationClient>(), Mock.Of<IEventPublisher>(),
                Mock.Of<IGenericRepository<OutboxMessage>>(),
                Mock.Of<ILogger<ProcessAiAnalysisResultCommandHandler>>(),
                Settings.Object);
        }

        public Task<AiAnalysisCallbackResponseDto> Handle(string status, Guid? mediaId = null,
            List<DetectionDto>? detections = null) => _handler.Handle(new()
        {
            RequestId = Request.Id, MediaId = mediaId ?? MediaId, MissionId = MissionId,
            Status = status, ModelName = status == "Completed" ? "server" : null,
            Detections = status == "Completed" ? detections ?? [] : null,
            ErrorCode = status == "Failed" ? "AI_PROCESSING_FAILED" : null,
            ErrorMessage = status == "Failed" ? "worker failure" : null,
            CompletedAt = DateTime.UtcNow
        }, CancellationToken.None);
    }
}
