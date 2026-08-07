using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Infrastructure.Messaging;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class AIAnalysisResultConsumerTests
{
    private readonly Mock<ILogger<AIAnalysisResultConsumer>> _loggerMock = new();
    private readonly Mock<RabbitMqConnection> _rabbitMqConnectionMock = new(Mock.Of<IConfiguration>());
    private readonly Mock<IChannel> _channelMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly AIAnalysisResultConsumer _consumer;

    public AIAnalysisResultConsumerTests()
    {
        _scopeMock.SetupGet(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ISender))).Returns(_senderMock.Object);

        _consumer = new AIAnalysisResultConsumer(
            _loggerMock.Object,
            _rabbitMqConnectionMock.Object,
            _scopeFactoryMock.Object,
            Options.Create(new AIAnalysisResultMessagingOptions()));
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldAck_WhenPayloadIsValid()
    {
        var evt = CreateEvent();
        var body = JsonSerializer.SerializeToUtf8Bytes(evt);
        var args = new BasicDeliverEventArgs
        {
            DeliveryTag = 7,
            Body = body,
            BasicProperties = new BasicProperties()
        };

        _senderMock
            .Setup(x => x.Send(It.IsAny<ProcessAiAnalysisResultCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnalysisCallbackResponseDto { RequestId = evt.AnalysisId, Status = "Completed" });
        _channelMock
            .Setup(x => x.BasicAckAsync(7, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _consumer.HandleMessageAsync(args, _channelMock.Object, CancellationToken.None);

        _senderMock.Verify(x => x.Send(It.Is<ProcessAiAnalysisResultCommand>(c =>
            c.RequestId == evt.AnalysisId &&
            c.MediaId == evt.MediaId &&
            c.Status == "Completed" &&
            c.Detections != null &&
            c.Detections.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _channelMock.Verify(x => x.BasicAckAsync(7, false, It.IsAny<CancellationToken>()), Times.Once);
        _channelMock.Verify(x => x.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldNack_WhenPayloadIsInvalid()
    {
        var args = new BasicDeliverEventArgs
        {
            DeliveryTag = 11,
            Body = Encoding.UTF8.GetBytes("{bad json"),
            BasicProperties = new BasicProperties()
        };

        _channelMock
            .Setup(x => x.BasicNackAsync(11, false, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _consumer.HandleMessageAsync(args, _channelMock.Object, CancellationToken.None);

        _senderMock.Verify(x => x.Send(It.IsAny<ProcessAiAnalysisResultCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _channelMock.Verify(x => x.BasicNackAsync(11, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldRepublishToRetryQueue_WhenTransientErrorOccurs()
    {
        var evt = CreateEvent();
        var props = new BasicProperties
        {
            Headers = new Dictionary<string, object?>()
        };
        var args = new BasicDeliverEventArgs
        {
            DeliveryTag = 13,
            Body = JsonSerializer.SerializeToUtf8Bytes(evt),
            BasicProperties = props
        };

        _senderMock
            .Setup(x => x.Send(It.IsAny<ProcessAiAnalysisResultCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("transient"));
        _channelMock
            .Setup(x => x.BasicPublishAsync(string.Empty, "ai.analysis.result.retry", false, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _channelMock
            .Setup(x => x.BasicAckAsync(13, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _consumer.HandleMessageAsync(args, _channelMock.Object, CancellationToken.None);

        _channelMock.Verify(x => x.BasicPublishAsync(string.Empty, "ai.analysis.result.retry", false, It.Is<BasicProperties>(p =>
            p.Headers != null && p.Headers.ContainsKey("x-retry-count")), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
        _channelMock.Verify(x => x.BasicAckAsync(13, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldNack_WhenBusinessRuleViolationOccurs()
    {
        var evt = CreateEvent();
        var args = new BasicDeliverEventArgs
        {
            DeliveryTag = 17,
            Body = JsonSerializer.SerializeToUtf8Bytes(evt),
            BasicProperties = new BasicProperties()
        };

        _senderMock
            .Setup(x => x.Send(It.IsAny<ProcessAiAnalysisResultCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("bad payload"));
        _channelMock
            .Setup(x => x.BasicNackAsync(17, false, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _consumer.HandleMessageAsync(args, _channelMock.Object, CancellationToken.None);

        _channelMock.Verify(x => x.BasicNackAsync(17, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AIAnalysisResultEvent CreateEvent() => new()
    {
        EventId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        AnalysisId = Guid.NewGuid(),
        InspectionId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Status = "Completed",
        ModelName = "HarnessRuntime",
        ModelVersion = "1.0.0",
        ProcessingTimeMs = 120,
        ProcessedAt = DateTime.UtcNow,
        Results =
        [
            new AIAnalysisResultDetectionEvent
            {
                Id = Guid.NewGuid().ToString(),
                CategoryCode = "CI",
                Confidence = 0.9,
                BoundingBox = new AIAnalysisResultBoundingBoxEvent
                {
                    X = 0.1,
                    Y = 0.2,
                    Width = 0.3,
                    Height = 0.4
                }
            }
        ]
    };
}
