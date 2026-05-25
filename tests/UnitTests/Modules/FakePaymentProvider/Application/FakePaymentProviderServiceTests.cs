using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.UnitTests.Modules.FakePaymentProvider.Application;

public sealed class FakePaymentProviderServiceTests
{
    [Fact]
    public async Task PayAsync_WhenSameApprovedIdempotencyKeyIsUsed_ShouldReturnSameProviderTransaction()
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest(
            "PAYMENT:order-1",
            Guid.Parse("c3a45cf4-3989-44da-a816-8db5a50cb389"),
            199.90m,
            "BRL",
            "Approved");

        var first = await service.PayAsync(request, CancellationToken.None);
        var second = await service.PayAsync(request with { ForceResult = "Rejected" }, CancellationToken.None);

        first.Status.Should().Be(FakePaymentProviderStatus.Approved);
        second.Status.Should().Be(FakePaymentProviderStatus.Approved);
        second.ProviderTransactionId.Should().Be(first.ProviderTransactionId);
    }

    [Fact]
    public async Task PayAsync_WhenTemporaryFailureOccurs_ShouldNotCacheRetryableResult()
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest(
            "PAYMENT:order-2",
            Guid.Parse("38841373-601c-4320-ad96-47240ad08bcc"),
            199.90m,
            "BRL",
            "TemporaryFailure");

        var temporaryFailure = await service.PayAsync(request, CancellationToken.None);
        var approved = await service.PayAsync(request with { ForceResult = "Approved" }, CancellationToken.None);

        temporaryFailure.Status.Should().Be(FakePaymentProviderStatus.TemporaryFailure);
        approved.Status.Should().Be(FakePaymentProviderStatus.Approved);
    }

    [Fact]
    public async Task PayAsync_WhenSameRejectedIdempotencyKeyIsUsed_ShouldReturnSameRejectedResult()
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest(
            "PAYMENT:order-3",
            Guid.Parse("e8dfad98-79a6-4d0d-bcfc-d7a7a3f07ed8"),
            199.90m,
            "BRL",
            "Rejected");

        var first = await service.PayAsync(request, CancellationToken.None);
        var second = await service.PayAsync(request with { ForceResult = "Approved" }, CancellationToken.None);

        first.Status.Should().Be(FakePaymentProviderStatus.Rejected);
        second.Status.Should().Be(FakePaymentProviderStatus.Rejected);
        second.ResponseCode.Should().Be("CARD_DECLINED");
    }

    [Fact]
    public async Task PayAsync_WhenTimeoutOccurs_ShouldNotCacheRetryableResult()
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest(
            "PAYMENT:order-4",
            Guid.Parse("c56e724b-565a-4d41-950d-78e29729d0d6"),
            199.90m,
            "BRL",
            "Timeout");

        var timeout = await service.PayAsync(request, CancellationToken.None);
        var approved = await service.PayAsync(request with { ForceResult = "Approved" }, CancellationToken.None);

        timeout.Status.Should().Be(FakePaymentProviderStatus.Timeout);
        approved.Status.Should().Be(FakePaymentProviderStatus.Approved);
    }

    [Theory]
    [InlineData("", 199.90, "BRL", "idempotencyKey is required")]
    [InlineData("PAYMENT:order-5", 0, "BRL", "amount must be greater than zero")]
    [InlineData("PAYMENT:order-5", 199.90, "B", "currency must use a 3-letter ISO code")]
    public async Task PayAsync_WhenRequestIsInvalid_ShouldThrowArgumentException(
        string idempotencyKey,
        decimal amount,
        string currency,
        string expectedMessage)
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest(idempotencyKey, Guid.NewGuid(), amount, currency, "Approved");

        Func<Task> act = () => service.PayAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedMessage);
    }

    [Fact]
    public async Task PayAsync_WhenForceResultIsInvalid_ShouldThrowArgumentException()
    {
        var service = CreateService();
        var request = new FakePaymentProviderRequest("PAYMENT:order-6", Guid.NewGuid(), 199.90m, "BRL", "Unknown");

        Func<Task> act = () => service.PayAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("forceResult must be Approved, TemporaryFailure, Rejected or Timeout");
    }

    [Theory]
    [InlineData("AlwaysApprove", FakePaymentProviderStatus.Approved)]
    [InlineData("AlwaysTemporaryFail", FakePaymentProviderStatus.TemporaryFailure)]
    [InlineData("AlwaysReject", FakePaymentProviderStatus.Rejected)]
    [InlineData("Timeout", FakePaymentProviderStatus.Timeout)]
    public async Task PayAsync_WhenModeSelectsStrategy_ShouldReturnStrategyStatus(string mode, FakePaymentProviderStatus expectedStatus)
    {
        var service = CreateService(new FakePaymentProviderOptions { Mode = mode, ArtificialDelayMs = 0 });
        var request = new FakePaymentProviderRequest($"PAYMENT:{mode}", Guid.NewGuid(), 199.90m, "BRL", null);

        var response = await service.PayAsync(request, CancellationToken.None);

        response.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task PayAsync_WhenRandomStrategyHasSuccessRate100_ShouldApprove()
    {
        var service = CreateService(new FakePaymentProviderOptions { Mode = "Random", SuccessRate = 100, ArtificialDelayMs = 0 });
        var request = new FakePaymentProviderRequest("PAYMENT:random", Guid.NewGuid(), 199.90m, "BRL", null);

        var response = await service.PayAsync(request, CancellationToken.None);

        response.Status.Should().Be(FakePaymentProviderStatus.Approved);
    }

    private static FakePaymentProviderService CreateService(FakePaymentProviderOptions? options = null) =>
        new(
            [
                new RandomPaymentProviderStrategy(Options.Create(options ?? new FakePaymentProviderOptions())),
                new AlwaysApprovePaymentProviderStrategy(),
                new AlwaysTemporaryFailPaymentProviderStrategy(),
                new AlwaysRejectPaymentProviderStrategy(),
                new TimeoutPaymentProviderStrategy()
            ],
            Options.Create(options ?? new FakePaymentProviderOptions { ArtificialDelayMs = 0 }),
            NullLogger<FakePaymentProviderService>.Instance);
}
