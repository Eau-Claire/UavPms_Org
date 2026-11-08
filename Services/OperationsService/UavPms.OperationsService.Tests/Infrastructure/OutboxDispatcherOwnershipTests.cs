using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UavPms.OperationsService.Infrastructure.Messaging;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Tests.Infrastructure;

public sealed class OutboxDispatcherOwnershipTests
{
    private static OutboxMessage Message(string type, DateTime? publishedAt = null) => new()
    {
        Id = Guid.NewGuid(), MessageType = type, Payload = "{}",
        OccurredAt = DateTime.UtcNow, PublishedAt = publishedAt
    };

    [Fact]
    public void PendingForDispatch_ShouldSkipMessagesOwnedByAiInspectionService()
    {
        var outbox = new List<OutboxMessage>
        {
            Message(nameof(InspectionMediaUploadedEvent)),
            Message("AIAnalysisRequestedEvent"),
            Message("DefectDetectedEvent"),
            Message("AiDetectionCompletedEvent"),
            Message("AIAnalysisStatusChangedEvent"),
            Message(nameof(MissionDispatchedEvent), DateTime.UtcNow)
        };

        var pending = OutboxDispatcher.PendingForDispatch(outbox.AsQueryable()).ToList();

        pending.Select(m => m.MessageType).Should().Equal(nameof(InspectionMediaUploadedEvent));
    }
}
