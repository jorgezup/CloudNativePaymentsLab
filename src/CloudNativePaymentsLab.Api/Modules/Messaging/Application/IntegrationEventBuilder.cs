using System.Text.Json;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public sealed class IntegrationEventBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OutboxMessage BuildOrderCreated(Order order, DateTimeOffset occurredAt)
    {
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope<OrderCreatedPayload>(
            messageId,
            "OrderCreated",
            order.Id,
            "Order",
            messageId,
            messageId,
            occurredAt,
            new OrderCreatedPayload(order.Id, order.CustomerId, order.Amount, order.Currency, order.IdempotencyKey));

        return new OutboxMessage(
            envelope.MessageId,
            envelope.AggregateId,
            envelope.AggregateType,
            envelope.EventType,
            JsonSerializer.Serialize(envelope, SerializerOptions),
            envelope.CorrelationId,
            envelope.CausationId,
            occurredAt);
    }
}
