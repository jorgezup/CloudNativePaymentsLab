namespace CloudNativePaymentsLab.Api.Modules.Payments.Domain;

public sealed class DeadLetterMessage
{
    private DeadLetterMessage()
    {
    }

    public DeadLetterMessage(
        Guid id,
        Guid originalMessageId,
        string topic,
        string consumerName,
        string eventType,
        Guid aggregateId,
        string payload,
        string errorMessage,
        int retryCount,
        DateTimeOffset createdAt)
    {
        Id = id;
        OriginalMessageId = originalMessageId;
        Topic = topic;
        ConsumerName = consumerName;
        EventType = eventType;
        AggregateId = aggregateId;
        Payload = payload;
        ErrorMessage = errorMessage.Length > 4000 ? errorMessage[..4000] : errorMessage;
        RetryCount = retryCount;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OriginalMessageId { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string ConsumerName { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string ErrorMessage { get; private set; } = string.Empty;
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
