using System.Text.Json;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public sealed class IntegrationEventBuilder(IOptions<KafkaOptions>? kafkaOptions = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaTopics topics = kafkaOptions?.Value.Topics ?? new KafkaTopics();

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
            topics.OrderCreated,
            JsonSerializer.Serialize(envelope, SerializerOptions),
            envelope.CorrelationId,
            envelope.CausationId,
            occurredAt);
    }

    public OutboxMessage BuildPaymentApproved(Payment payment, Guid correlationId, Guid causationId, DateTimeOffset occurredAt)
    {
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope<PaymentApprovedPayload>(
            messageId,
            "PaymentApproved",
            payment.OrderId,
            "Payment",
            correlationId,
            causationId,
            occurredAt,
            new PaymentApprovedPayload(
                payment.Id,
                payment.OrderId,
                payment.CustomerId,
                payment.Amount,
                payment.Currency,
                payment.IdempotencyKey,
                payment.ProviderTransactionId ?? string.Empty));

        return Build(envelope, topics.PaymentApproved, occurredAt);
    }

    public OutboxMessage BuildPaymentFailed(Payment payment, string reason, Guid correlationId, Guid causationId, DateTimeOffset occurredAt)
    {
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope<PaymentFailedPayload>(
            messageId,
            "PaymentFailed",
            payment.OrderId,
            "Payment",
            correlationId,
            causationId,
            occurredAt,
            new PaymentFailedPayload(
                payment.Id,
                payment.OrderId,
                payment.CustomerId,
                payment.Amount,
                payment.Currency,
                payment.IdempotencyKey,
                reason));

        return Build(envelope, topics.PaymentFailed, occurredAt);
    }

    public OutboxMessage BuildPaymentMovedToDeadLetter(Payment payment, string reason, Guid correlationId, Guid causationId, DateTimeOffset occurredAt)
    {
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope<PaymentMovedToDeadLetterPayload>(
            messageId,
            "PaymentMovedToDeadLetter",
            payment.OrderId,
            "Payment",
            correlationId,
            causationId,
            occurredAt,
            new PaymentMovedToDeadLetterPayload(payment.Id, payment.OrderId, payment.OriginalMessageId, reason, payment.AttemptCount));

        return Build(envelope, topics.PaymentsDeadLetter, occurredAt);
    }

    private static OutboxMessage Build<TPayload>(IntegrationEventEnvelope<TPayload> envelope, string topic, DateTimeOffset occurredAt) =>
        new(
            envelope.MessageId,
            envelope.AggregateId,
            envelope.AggregateType,
            envelope.EventType,
            topic,
            JsonSerializer.Serialize(envelope, SerializerOptions),
            envelope.CorrelationId,
            envelope.CausationId,
            occurredAt);
}
