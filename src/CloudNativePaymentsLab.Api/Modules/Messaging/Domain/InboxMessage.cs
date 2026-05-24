namespace CloudNativePaymentsLab.Api.Modules.Messaging.Domain;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public InboxMessage(Guid messageId, string consumerName, string eventType, Guid aggregateId, DateTimeOffset now)
    {
        MessageId = messageId;
        ConsumerName = consumerName;
        EventType = eventType;
        AggregateId = aggregateId;
        ProcessedAt = now;
        CreatedAt = now;
    }

    public Guid MessageId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
