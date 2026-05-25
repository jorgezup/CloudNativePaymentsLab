using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Domain;

public sealed class DeadLetterMessageTests
{
    [Fact]
    public void Constructor_WhenErrorIsLong_ShouldTruncateErrorMessage()
    {
        var error = new string('x', 4_100);

        var message = new DeadLetterMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "orders.order-created",
            "consumer",
            "OrderCreated",
            Guid.NewGuid(),
            "{}",
            error,
            5,
            DateTimeOffset.UtcNow);

        message.ErrorMessage.Should().HaveLength(4_000);
    }
}
