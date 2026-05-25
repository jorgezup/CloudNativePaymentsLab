using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Domain;

public sealed class PaymentAttemptTests
{
    [Fact]
    public void MarkApproved_WhenProviderApproves_ShouldStoreProviderFields()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var attempt = CreateAttempt(now);

        attempt.MarkApproved("provider-123", "00", "Payment approved", now.AddSeconds(1));

        attempt.Status.Should().Be(PaymentAttemptStatus.Approved);
        attempt.ProviderTransactionId.Should().Be("provider-123");
        attempt.ProviderResponseCode.Should().Be("00");
        attempt.ProviderResponseMessage.Should().Be("Payment approved");
        attempt.ErrorMessage.Should().BeNull();
        attempt.FinishedAt.Should().Be(now.AddSeconds(1));
    }

    [Theory]
    [InlineData("MarkPermanentError", PaymentAttemptStatus.PermanentError, "CARD_DECLINED")]
    [InlineData("MarkRetryableError", PaymentAttemptStatus.RetryableError, "TEMP_001")]
    public void MarkError_WhenProviderFails_ShouldStoreErrorFields(string method, PaymentAttemptStatus expectedStatus, string responseCode)
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var attempt = CreateAttempt(now);

        if (method == "MarkPermanentError")
        {
            attempt.MarkPermanentError(responseCode, "Payment rejected", now.AddSeconds(1));
        }
        else
        {
            attempt.MarkRetryableError(responseCode, "Temporary provider instability", now.AddSeconds(1));
        }

        attempt.Status.Should().Be(expectedStatus);
        attempt.ProviderResponseCode.Should().Be(responseCode);
        attempt.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        attempt.FinishedAt.Should().Be(now.AddSeconds(1));
    }

    [Fact]
    public void MarkTimeout_WhenProviderTimesOut_ShouldStoreTimeoutFields()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var attempt = CreateAttempt(now);

        attempt.MarkTimeout("Provider timeout", now.AddSeconds(1));

        attempt.Status.Should().Be(PaymentAttemptStatus.Timeout);
        attempt.ProviderResponseCode.Should().Be("TIMEOUT");
        attempt.ErrorMessage.Should().Be("Provider timeout");
    }

    [Fact]
    public void MarkRetryableError_WhenErrorIsLong_ShouldTruncateError()
    {
        var attempt = CreateAttempt(DateTimeOffset.UtcNow);
        var error = new string('x', 2_100);

        attempt.MarkRetryableError("TEMP_001", error, DateTimeOffset.UtcNow);

        attempt.ErrorMessage.Should().HaveLength(2_000);
    }

    private static PaymentAttempt CreateAttempt(DateTimeOffset now) =>
        PaymentAttempt.Start(Guid.NewGuid(), Guid.NewGuid(), 1, "PAYMENT:order-1", now);
}
