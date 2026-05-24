using System.Text.Json;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
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
}
