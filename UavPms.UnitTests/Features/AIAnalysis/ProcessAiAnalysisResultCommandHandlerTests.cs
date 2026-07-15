using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using Xunit;

namespace UavPms.UnitTests.Features.AIAnalysis;

public class ProcessAiAnalysisResultCommandHandlerTests
{
    private readonly Mock<IGenericRepository<AIAnalysisRequest>> _aiRequestRepoMock;
    private readonly Mock<IInspectionMediaRepository> _mediaRepoMock;
    private readonly Mock<IGenericRepository<DefectCategory>> _defectCategoryRepoMock;
    private readonly Mock<IAnomalyRepository> _anomalyRepoMock;
    private readonly Mock<IGenericRepository<EmergencyAlert>> _emergencyAlertRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ProcessAiAnalysisResultCommandHandler>> _loggerMock;

    private readonly ProcessAiAnalysisResultCommandHandler _handler;

    public ProcessAiAnalysisResultCommandHandlerTests()
    {
        _aiRequestRepoMock = new Mock<IGenericRepository<AIAnalysisRequest>>();
        _mediaRepoMock = new Mock<IInspectionMediaRepository>();
        _defectCategoryRepoMock = new Mock<IGenericRepository<DefectCategory>>();
        _anomalyRepoMock = new Mock<IAnomalyRepository>();
        _emergencyAlertRepoMock = new Mock<IGenericRepository<EmergencyAlert>>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ProcessAiAnalysisResultCommandHandler>>();

        _handler = new ProcessAiAnalysisResultCommandHandler(
            _aiRequestRepoMock.Object,
            _mediaRepoMock.Object,
            _defectCategoryRepoMock.Object,
            _anomalyRepoMock.Object,
            _emergencyAlertRepoMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenAIAnalysisRequestDoesNotExist()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync((AIAnalysisRequest?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{nameof(AIAnalysisRequest)}*");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnCompletedResponse_WhenAIAnalysisRequestAlreadyCompleted()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Completed
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RequestId.Should().Be(command.RequestId);
        result.Status.Should().Be("Completed");
        result.SavedDetections.Should().Be(0);
        result.CreatedAlerts.Should().Be(0);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenInspectionMediaDoesNotExist()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Pending
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        _mediaRepoMock.Setup(r => r.GetByIdWithDetailsAsync(command.MediaId.Value))
            .ReturnsAsync((InspectionMedia?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{nameof(InspectionMedia)}*");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenDefectCategoryDoesNotExist()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            ModelName = "YOLOv8",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    CategoryCode = "UNKNOWN_CODE",
                    Confidence = 0.9,
                    BoundingBox = new BoundingBoxDto { X = 0.1, Y = 0.1, Width = 0.5, Height = 0.5 }
                }
            },
            CompletedAt = DateTime.UtcNow
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Pending
        };

        var existingMedia = new InspectionMedia
        {
            Id = command.MediaId.Value,
            AssetId = Guid.NewGuid()
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        _mediaRepoMock.Setup(r => r.GetByIdWithDetailsAsync(command.MediaId.Value))
            .ReturnsAsync(existingMedia);

        _defectCategoryRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DefectCategory, bool>>>(), false))
            .ReturnsAsync(new List<DefectCategory>()); // empty list indicates category not found

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*UNKNOWN_CODE*");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProcessCompletedResultAndSaveDetections_WhenValidCompletedResult()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            ModelName = "RF-DETR",
            ModelVersion = "v1.0",
            ProcessingTimeMs = 1500,
            Detections = new List<DetectionDto>
            {
                new()
                {
                    CategoryCode = "BROKEN_INSULATOR",
                    Confidence = 0.75, // Lower than 0.80, so no emergency alert
                    BoundingBox = new BoundingBoxDto { X = 0.1, Y = 0.2, Width = 0.3, Height = 0.4 }
                }
            },
            CompletedAt = DateTime.UtcNow
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Pending
        };

        var existingMedia = new InspectionMedia
        {
            Id = command.MediaId.Value,
            AssetId = Guid.NewGuid()
        };

        var defectCategory = new DefectCategory
        {
            Id = 10,
            CategoryCode = "BROKEN_INSULATOR",
            CategoryName = "Broken Insulator",
            IsEmergencyClass = false
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        _mediaRepoMock.Setup(r => r.GetByIdWithDetailsAsync(command.MediaId.Value))
            .ReturnsAsync(existingMedia);

        _defectCategoryRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DefectCategory, bool>>>(), false))
            .ReturnsAsync(new List<DefectCategory> { defectCategory });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SavedDetections.Should().Be(1);
        result.CreatedAlerts.Should().Be(0);
        result.Status.Should().Be("Completed");

        _anomalyRepoMock.Verify(r => r.AddAsync(It.Is<DetectedAnomaly>(a =>
            a.MediaId == command.MediaId &&
            a.CategoryId == defectCategory.Id &&
            a.ConfidenceScore == 0.75 &&
            a.AiSource == "RF-DETR"
        )), Times.Once);

        _mediaRepoMock.Verify(r => r.UpdateAsync(It.Is<InspectionMedia>(m =>
            m.AiSource == "RF-DETR" &&
            m.ValidationStatus == "PendingReview"
        )), Times.Once);

        existingRequest.Status.Should().Be(AIAnalysisStatus.Completed);
        existingRequest.CompletedAt.Should().Be(command.CompletedAt);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateEmergencyAlertAndNotifyManager_WhenDefectIsEmergencyClassAndConfidenceHigh()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Completed",
            ModelName = "RF-DETR",
            ModelVersion = "v1.0",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    CategoryCode = "VEGETATION_ENCROACHMENT",
                    Confidence = 0.92, // Higher than 0.80
                    BoundingBox = new BoundingBoxDto { X = 0.1, Y = 0.2, Width = 0.3, Height = 0.4 }
                }
            },
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Pending
        };

        var managerId = Guid.NewGuid();
        var existingMedia = new InspectionMedia
        {
            Id = command.MediaId.Value,
            AssetId = Guid.NewGuid(),
            MissionId = Guid.NewGuid(),
            Mission = new Mission
            {
                Id = Guid.NewGuid(),
                ManagerId = managerId
            }
        };

        var defectCategory = new DefectCategory
        {
            Id = 12,
            CategoryCode = "VEGETATION_ENCROACHMENT",
            CategoryName = "Vegetation Encroachment",
            IsEmergencyClass = true // Emergency!
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        _mediaRepoMock.Setup(r => r.GetByIdWithDetailsAsync(command.MediaId.Value))
            .ReturnsAsync(existingMedia);

        _defectCategoryRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DefectCategory, bool>>>(), false))
            .ReturnsAsync(new List<DefectCategory> { defectCategory });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SavedDetections.Should().Be(1);
        result.CreatedAlerts.Should().Be(1);

        _anomalyRepoMock.Verify(r => r.AddAsync(It.IsAny<DetectedAnomaly>()), Times.Once);

        _emergencyAlertRepoMock.Verify(r => r.AddAsync(It.Is<EmergencyAlert>(a =>
            a.AssetId == existingMedia.AssetId &&
            a.MissionId == existingMedia.MissionId &&
            a.Status == "Open" &&
            a.Priority == "Critical"
        )), Times.Once);

        _notificationRepoMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == managerId &&
            n.Type == "CriticalAlert" &&
            n.ReferenceType == "EmergencyAlert"
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProcessFailedResult_WhenStatusIsFailed()
    {
        // Arrange
        var command = new ProcessAiAnalysisResultCommand
        {
            RequestId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Status = "Failed",
            ErrorCode = "MODEL_INFERENCE_FAILED",
            ErrorMessage = "CUDA out of memory",
            CompletedAt = DateTime.UtcNow
        };

        var existingRequest = new AIAnalysisRequest
        {
            Id = command.RequestId,
            Status = AIAnalysisStatus.Pending
        };

        var existingMedia = new InspectionMedia
        {
            Id = command.MediaId.Value,
            AssetId = Guid.NewGuid()
        };

        _aiRequestRepoMock.Setup(r => r.GetByIdAsync(command.RequestId, true))
            .ReturnsAsync(existingRequest);

        _mediaRepoMock.Setup(r => r.GetByIdWithDetailsAsync(command.MediaId.Value))
            .ReturnsAsync(existingMedia);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SavedDetections.Should().Be(0);
        result.CreatedAlerts.Should().Be(0);
        result.Status.Should().Be("Failed");

        existingRequest.Status.Should().Be(AIAnalysisStatus.Failed);
        existingRequest.Result.Should().Contain("MODEL_INFERENCE_FAILED");

        _anomalyRepoMock.Verify(r => r.AddAsync(It.IsAny<DetectedAnomaly>()), Times.Never);
        _mediaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<InspectionMedia>()), Times.Never);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
