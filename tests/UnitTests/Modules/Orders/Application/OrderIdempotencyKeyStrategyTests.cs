using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Orders.Application;

public sealed class OrderIdempotencyKeyStrategyTests
{
    [Fact]
    public void Generate_WhenExternalReferenceIsProvided_ShouldReturnDeterministicTrimmedKey()
    {
        // Arrange
        var strategy = new OrderIdempotencyKeyStrategy();

        // Act
        var key = strategy.Generate(" customer-1 ", " reference-1 ");

        // Assert
        key.Should().Be("ORDER:customer-1:reference-1");
    }

    [Fact]
    public void Generate_WhenExternalReferenceIsMissing_ShouldReturnInternalReferenceKey()
    {
        // Arrange
        var strategy = new OrderIdempotencyKeyStrategy();

        // Act
        var key = strategy.Generate(" customer-1 ", " ");

        // Assert
        key.Should().StartWith("ORDER:customer-1:internal-");
        key.Should().HaveLength("ORDER:customer-1:internal-".Length + 32);
    }
}
