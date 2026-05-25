using System.Text.Json;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Messaging.Application;

public sealed class IntegrationEventBuilderTests
{
    [Fact]
    public void BuildOrderCreated_WhenOrderIsProvided_ShouldCreatePendingOutboxMessageWithSerializedEnvelope()
    {
        // Arrange
        var occurredAt = new DateTimeOffset(2026, 5, 24, 10, 30, 0, TimeSpan.Zero);
        var order = Order.Create("customer-1", 150.75m, "brl", "ORDER:customer-1:reference-1", occurredAt);
        var builder = new IntegrationEventBuilder();

        // Act
        var message = builder.BuildOrderCreated(order, occurredAt);

        // Assert
        message.AggregateId.Should().Be(order.Id);
        message.AggregateType.Should().Be("Order");
        message.EventType.Should().Be("OrderCreated");
        message.Topic.Should().Be("orders.order-created");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.CreatedAt.Should().Be(occurredAt);
        message.CorrelationId.Should().Be(message.Id);
        message.CausationId.Should().Be(message.Id);

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<OrderCreatedPayload>>(
            message.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        envelope.Should().NotBeNull();
        envelope!.MessageId.Should().Be(message.Id);
        envelope.EventType.Should().Be("OrderCreated");
        envelope.AggregateId.Should().Be(order.Id);
        envelope.AggregateType.Should().Be("Order");
        envelope.CorrelationId.Should().Be(message.Id);
        envelope.CausationId.Should().Be(message.Id);
        envelope.OccurredAt.Should().Be(occurredAt);
        envelope.Payload.Should().Be(new OrderCreatedPayload(order.Id, order.CustomerId, order.Amount, order.Currency, order.IdempotencyKey));
    }

    [Fact]
    public void BuildPaymentApproved_WhenPaymentIsProvided_ShouldCreatePaymentApprovedMessage()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 24, 10, 30, 0, TimeSpan.Zero);
        var payment = CreateApprovedPayment(occurredAt);
        var builder = new IntegrationEventBuilder();

        var message = builder.BuildPaymentApproved(payment, Guid.Parse("4d0e11d6-7cc1-4ed6-a115-8be680f0ddc7"), Guid.Parse("ec52ded2-c963-4aa7-a5e4-d51c4fa84f9f"), occurredAt);

        message.EventType.Should().Be("PaymentApproved");
        message.Topic.Should().Be("payments.payment-approved");
        message.AggregateType.Should().Be("Payment");

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<PaymentApprovedPayload>>(
            message.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        envelope.Should().NotBeNull();
        envelope!.Payload.PaymentId.Should().Be(payment.Id);
        envelope.Payload.ProviderTransactionId.Should().Be("provider-123");
    }

    [Fact]
    public void BuildPaymentFailed_WhenPaymentIsProvided_ShouldCreatePaymentFailedMessage()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 24, 10, 30, 0, TimeSpan.Zero);
        var payment = CreatePayment(occurredAt);
        var builder = new IntegrationEventBuilder();

        var message = builder.BuildPaymentFailed(payment, "Payment rejected", Guid.NewGuid(), Guid.NewGuid(), occurredAt);

        message.EventType.Should().Be("PaymentFailed");
        message.Topic.Should().Be("payments.payment-failed");

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<PaymentFailedPayload>>(
            message.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        envelope.Should().NotBeNull();
        envelope!.Payload.PaymentId.Should().Be(payment.Id);
        envelope.Payload.Reason.Should().Be("Payment rejected");
    }

    [Fact]
    public void BuildPaymentMovedToDeadLetter_WhenPaymentIsProvided_ShouldCreateDlqMessage()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 24, 10, 30, 0, TimeSpan.Zero);
        var payment = CreatePayment(occurredAt);
        payment.MarkDeadLettered("Maximum attempts exceeded", occurredAt);
        var builder = new IntegrationEventBuilder();

        var message = builder.BuildPaymentMovedToDeadLetter(payment, "Maximum attempts exceeded", Guid.NewGuid(), Guid.NewGuid(), occurredAt);

        message.EventType.Should().Be("PaymentMovedToDeadLetter");
        message.Topic.Should().Be("payments.dlq");

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<PaymentMovedToDeadLetterPayload>>(
            message.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        envelope.Should().NotBeNull();
        envelope!.Payload.PaymentId.Should().Be(payment.Id);
        envelope.Payload.OriginalMessageId.Should().Be(payment.OriginalMessageId);
    }

    private static Payment CreateApprovedPayment(DateTimeOffset now)
    {
        var payment = CreatePayment(now);
        payment.MarkApproved("provider-123", now);
        return payment;
    }

    private static Payment CreatePayment(DateTimeOffset now)
    {
        var orderId = Guid.Parse("4c599b68-1a2f-4e28-b4ab-bd20f912abec");
        return Payment.Create(
            orderId,
            "customer-1",
            199.90m,
            "BRL",
            $"PAYMENT:{orderId}",
            Guid.Parse("5ac667ab-8069-4da5-ae1e-bd0f9f4e62bb"),
            "orders.order-created",
            "OrderCreated",
            now);
    }
}
