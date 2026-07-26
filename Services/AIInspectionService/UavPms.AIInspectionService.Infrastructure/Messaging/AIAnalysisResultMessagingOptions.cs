namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public class AIAnalysisResultMessagingOptions
{
    public const string SectionName = "AIAnalysisResultMessaging";

    public string ExchangeName { get; set; } = "identity-exchange";
    public string RoutingKey { get; set; } = "identity.event.aianalysisresultevent";
    public string QueueName { get; set; } = "ai.analysis.result";
    public string RetryQueueName { get; set; } = "ai.analysis.result.retry";
    public string DeadLetterExchangeName { get; set; } = "ai.analysis.result.dlx";
    public string DeadLetterQueueName { get; set; } = "ai.analysis.result.dead-letter";
    public int RetryLimit { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 15000;
    public ushort PrefetchCount { get; set; } = 1;
}
