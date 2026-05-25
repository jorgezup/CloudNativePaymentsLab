using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Messaging.Domain;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Constructor_WhenMessageIsCreated_ShouldInitializeAsPending()
    {
        // Arrange
        var id = Guid.Parse("2d9e03f3-0c69-4a97-844b-8b78a7f7ef01");
        var aggregateId = Guid.Parse("a14f306d-3d80-454c-8e79-724f20b0e5f3");
        var correlationId = Guid.Parse("eb9d30f1-cf0c-4f6d-ae2f-3d53e9f1992f");
        var causationId = Guid.Parse("a9651a72-6fef-414a-ad73-0fa51a3e8189");
        var createdAt = new DateTimeOffset(2026, 5, 24, 11, 0, 0, TimeSpan.Zero);

        // Act
        var message = new OutboxMessage(id, aggregateId, "Order", "OrderCreated", "orders.order-created", "{}", correlationId, causationId, createdAt);

        // Assert
        message.Id.Should().Be(id);
        message.AggregateId.Should().Be(aggregateId);
        message.AggregateType.Should().Be("Order");
        message.EventType.Should().Be("OrderCreated");
        message.Topic.Should().Be("orders.order-created");
        message.Payload.Should().Be("{}");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.RetryCount.Should().Be(0);
        message.PublishedAt.Should().BeNull();
        message.LastError.Should().BeNull();
        message.CorrelationId.Should().Be(correlationId);
        message.CausationId.Should().Be(causationId);
        message.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void MarkAsProcessing_WhenMessageIsPending_ShouldSetProcessingAndClearLastError()
    {
        // Arrange
        var message = CreateMessage();
        message.MarkAsFailed("temporary failure");

        // Act
        message.MarkAsProcessing();

        // Assert
        message.Status.Should().Be(OutboxMessageStatus.Processing);
        message.LastError.Should().BeNull();
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkAsPublished_WhenMessageIsProcessing_ShouldSetPublishedAtAndClearLastError()
    {
        // Arrange
        var publishedAt = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var message = CreateMessage();
        message.MarkAsFailed("temporary failure");
        message.MarkAsProcessing();

        // Act
        message.MarkAsPublished(publishedAt);

        // Assert
        message.Status.Should().Be(OutboxMessageStatus.Published);
        message.PublishedAt.Should().Be(publishedAt);
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_WhenErrorIsLongerThanLimit_ShouldIncrementRetryAndTruncateError()
    {
        // Arrange
        var message = CreateMessage();
        var error = new string('x', 2_100);

        // Act
        message.MarkAsFailed(error);

        // Assert
        message.Status.Should().Be(OutboxMessageStatus.Failed);
        message.RetryCount.Should().Be(1);
        message.LastError.Should().HaveLength(2_000);
        message.LastError.Should().Be(error[..2_000]);
    }

    private static OutboxMessage CreateMessage() =>
        new(
            Guid.Parse("2d9e03f3-0c69-4a97-844b-8b78a7f7ef01"),
            Guid.Parse("a14f306d-3d80-454c-8e79-724f20b0e5f3"),
            "Order",
            "OrderCreated",
            "orders.order-created",
            "{}",
            Guid.Parse("eb9d30f1-cf0c-4f6d-ae2f-3d53e9f1992f"),
            Guid.Parse("a9651a72-6fef-414a-ad73-0fa51a3e8189"),
            new DateTimeOffset(2026, 5, 24, 11, 0, 0, TimeSpan.Zero));
}
