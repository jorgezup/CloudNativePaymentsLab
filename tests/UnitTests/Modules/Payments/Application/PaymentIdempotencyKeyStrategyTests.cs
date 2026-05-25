using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Application;

public sealed class PaymentIdempotencyKeyStrategyTests
{
    [Fact]
    public void Generate_WhenOrderIdIsProvided_ShouldReturnDeterministicPaymentKey()
    {
        var orderId = Guid.Parse("8fdb8e14-9e63-456b-9fd4-cf9bb8b50c0a");
        var strategy = new PaymentIdempotencyKeyStrategy();

        var key = strategy.Generate(orderId);

        key.Should().Be("PAYMENT:8fdb8e14-9e63-456b-9fd4-cf9bb8b50c0a");
    }
}
