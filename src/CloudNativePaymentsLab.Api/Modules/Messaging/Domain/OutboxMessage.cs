namespace CloudNativePaymentsLab.Api.Modules.Messaging.Domain;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        Guid aggregateId,
        string aggregateType,
        string eventType,
        string payload,
        Guid correlationId,
        Guid causationId,
        DateTimeOffset createdAt)
    {
        Id = id;
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        EventType = eventType;
        Payload = payload;
        Status = OutboxMessageStatus.Pending;
        RetryCount = 0;
        CorrelationId = correlationId;
        CausationId = causationId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public OutboxMessageStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? LastError { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid CausationId { get; private set; }

    public void MarkAsProcessing()
    {
        Status = OutboxMessageStatus.Processing;
        LastError = null;
    }

    public void MarkAsPublished(DateTimeOffset now)
    {
        Status = OutboxMessageStatus.Published;
        PublishedAt = now;
        LastError = null;
    }

    public void MarkAsFailed(string error)
    {
        Status = OutboxMessageStatus.Failed;
        RetryCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
