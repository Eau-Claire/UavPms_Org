using System.Reflection;
using FluentAssertions;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Infrastructure.Messaging;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Infrastructure.Messaging;

public class RabbitMqEventPublisherTests
{
    [Theory]
    [InlineData("Image", "SERVER", "identity.event.aianalysisrequestedevent.server.image")]
    [InlineData("Video", "SERVER", "identity.event.aianalysisrequestedevent.server.video")]
    [InlineData("Image", "EDGE", "identity.event.aianalysisrequestedevent.edge.image")]
    [InlineData("Video", "YOLO11", "identity.event.aianalysisrequestedevent.edge.video")]
    public void AIAnalysisRequestedEvent_ShouldRouteByRuntimeAndMediaType(
        string mediaType,
        string preferredModel,
        string expectedRoutingKey)
    {
        var method = typeof(RabbitMqEventPublisher).GetMethod(
            "GetRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var eventPayload = new AIAnalysisRequestedEvent
        {
            MediaType = mediaType,
            PreferredModel = preferredModel
        };

        var routingKey = method!
            .MakeGenericMethod(typeof(AIAnalysisRequestedEvent))
            .Invoke(null, new object[] { nameof(AIAnalysisRequestedEvent), eventPayload });

        routingKey.Should().Be(expectedRoutingKey);
    }
}
