using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Messaging.Domain;

public sealed class InboxMessageTests
{
    [Fact]
    public void Constructor_WhenMessageIsCreated_ShouldInitializeProcessedMessage()
    {
        // Arrange
        var messageId = Guid.Parse("19fce944-7239-4d72-8ab4-3a8cf2aa522d");
        var aggregateId = Guid.Parse("a14f306d-3d80-454c-8e79-724f20b0e5f3");
        var now = new DateTimeOffset(2026, 5, 24, 13, 0, 0, TimeSpan.Zero);

        // Act
        var message = new InboxMessage(messageId, "consumer-1", "OrderCreated", aggregateId, now);

        // Assert
        message.MessageId.Should().Be(messageId);
        message.ConsumerName.Should().Be("consumer-1");
        message.EventType.Should().Be("OrderCreated");
        message.AggregateId.Should().Be(aggregateId);
        message.ProcessedAt.Should().Be(now);
        message.CreatedAt.Should().Be(now);
    }
}
