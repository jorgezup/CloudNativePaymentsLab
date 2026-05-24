namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public sealed record IntegrationEventEnvelope<TPayload>(
    Guid MessageId,
    string EventType,
    Guid AggregateId,
    string AggregateType,
    Guid CorrelationId,
    Guid CausationId,
    DateTimeOffset OccurredAt,
    TPayload Payload);

public sealed record OrderCreatedPayload(
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);
