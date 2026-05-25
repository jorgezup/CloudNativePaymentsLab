using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Domain;

public sealed class PaymentTests
{
    [Fact]
    public void ScheduleRetry_WhenTemporaryFailureOccurs_ShouldTrackAttemptAndNextRetry()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var payment = Payment.Create(
            Guid.NewGuid(),
            "customer-1",
            199.90m,
            "brl",
            "PAYMENT:order-1",
            Guid.NewGuid(),
            "orders.order-created",
            "OrderCreated",
            now);

        payment.MarkAttemptStarted(now);
        payment.ScheduleRetry("Temporary provider instability", now.AddSeconds(5), now);

        payment.AttemptCount.Should().Be(1);
        payment.Status.Should().Be(PaymentStatus.RetryScheduled);
        payment.NextRetryAt.Should().Be(now.AddSeconds(5));
    }

    [Fact]
    public void MarkApproved_WhenProviderApproves_ShouldStoreProviderTransactionId()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var payment = Payment.Create(
            Guid.NewGuid(),
            "customer-1",
            199.90m,
            "brl",
            "PAYMENT:order-1",
            Guid.NewGuid(),
            "orders.order-created",
            "OrderCreated",
            now);

        payment.MarkApproved("provider-123", now.AddSeconds(1));

        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.ProviderTransactionId.Should().Be("provider-123");
        payment.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_WhenErrorIsLong_ShouldTruncateErrorAndClearRetry()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var payment = CreatePayment(now);
        payment.ScheduleRetry("temporary", now.AddSeconds(5), now);
        var error = new string('x', 2_100);

        payment.MarkFailed(error, now.AddSeconds(1));

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.NextRetryAt.Should().BeNull();
        payment.LastError.Should().HaveLength(2_000);
    }

    [Fact]
    public void MarkDeadLettered_WhenErrorIsLong_ShouldTruncateErrorAndClearRetry()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var payment = CreatePayment(now);
        payment.ScheduleRetry("temporary", now.AddSeconds(5), now);
        var error = new string('x', 2_100);

        payment.MarkDeadLettered(error, now.AddSeconds(1));

        payment.Status.Should().Be(PaymentStatus.DeadLettered);
        payment.NextRetryAt.Should().BeNull();
        payment.LastError.Should().HaveLength(2_000);
    }

    private static Payment CreatePayment(DateTimeOffset now) =>
        Payment.Create(
            Guid.NewGuid(),
            "customer-1",
            199.90m,
            "brl",
            "PAYMENT:order-1",
            Guid.NewGuid(),
            "orders.order-created",
            "OrderCreated",
            now);
}
