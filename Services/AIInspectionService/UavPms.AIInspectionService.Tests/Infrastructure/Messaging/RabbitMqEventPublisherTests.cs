using System.Reflection;
using FluentAssertions;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Infrastructure.Messaging;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Infrastructure.Messaging;

public class RabbitMqEventPublisherTests
{
    [Fact]
    public void AIAnalysisRequestTopology_ShouldContainSixDistinctExactBindings()
    {
        var expected = new Dictionary<string, string>
        {
            ["ai.analysis.server.requested"] = "identity.event.aianalysisrequestedevent.server",
            ["ai.analysis.server.image.requested"] = "identity.event.aianalysisrequestedevent.server.image",
            ["ai.analysis.server.video.requested"] = "identity.event.aianalysisrequestedevent.server.video",
            ["ai.analysis.edge.requested"] = "identity.event.aianalysisrequestedevent.edge",
            ["ai.analysis.edge.image.requested"] = "identity.event.aianalysisrequestedevent.edge.image",
            ["ai.analysis.edge.video.requested"] = "identity.event.aianalysisrequestedevent.edge.video"
        };

        AIAnalysisRequestTopology.Routes.Should().HaveCount(6);
        AIAnalysisRequestTopology.Routes
            .ToDictionary(route => route.QueueName, route => route.RoutingKey)
            .Should().BeEquivalentTo(expected);
        AIAnalysisRequestTopology.Routes.Select(route => route.QueueName).Should().OnlyHaveUniqueItems();
        AIAnalysisRequestTopology.Routes.Select(route => route.RoutingKey).Should().OnlyHaveUniqueItems();
        AIAnalysisRequestTopology.Routes.Should().OnlyContain(route =>
            !route.RoutingKey.Contains('*') && !route.RoutingKey.Contains('#'));
    }

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
