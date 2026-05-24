using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Orders.Domain;

public sealed class OrderTests
{
    [Fact]
    public void Create_WhenValuesAreProvided_ShouldInitializeCreatedOrder()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero);

        // Act
        var order = Order.Create("customer-1", 99.90m, "brl", "ORDER:customer-1:reference-1", now);

        // Assert
        order.Id.Should().NotBeEmpty();
        order.CustomerId.Should().Be("customer-1");
        order.Amount.Should().Be(99.90m);
        order.Currency.Should().Be("BRL");
        order.Status.Should().Be(OrderStatus.Created);
        order.IdempotencyKey.Should().Be("ORDER:customer-1:reference-1");
        order.CreatedAt.Should().Be(now);
        order.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void MarkAsProcessing_WhenOrderIsCreated_ShouldChangeStatusAndUpdatedAt()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero);
        var processingAt = new DateTimeOffset(2026, 5, 24, 10, 5, 0, TimeSpan.Zero);
        var order = Order.Create("customer-1", 99.90m, "BRL", "ORDER:customer-1:reference-1", createdAt);

        // Act
        order.MarkAsProcessing(processingAt);

        // Assert
        order.Status.Should().Be(OrderStatus.Processing);
        order.UpdatedAt.Should().Be(processingAt);
    }

    [Fact]
    public void MarkAsProcessing_WhenOrderIsAlreadyProcessing_ShouldKeepCurrentUpdatedAt()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero);
        var firstProcessingAt = new DateTimeOffset(2026, 5, 24, 10, 5, 0, TimeSpan.Zero);
        var secondProcessingAt = new DateTimeOffset(2026, 5, 24, 10, 10, 0, TimeSpan.Zero);
        var order = Order.Create("customer-1", 99.90m, "BRL", "ORDER:customer-1:reference-1", createdAt);
        order.MarkAsProcessing(firstProcessingAt);

        // Act
        order.MarkAsProcessing(secondProcessingAt);

        // Assert
        order.Status.Should().Be(OrderStatus.Processing);
        order.UpdatedAt.Should().Be(firstProcessingAt);
    }
}
