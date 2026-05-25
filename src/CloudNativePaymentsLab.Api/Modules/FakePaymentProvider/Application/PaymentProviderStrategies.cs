using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

public sealed class RandomPaymentProviderStrategy(IOptions<FakePaymentProviderOptions> options) : IPaymentProviderStrategy
{
    public FakePaymentProviderStatus Choose(FakePaymentProviderRequest request)
    {
        var roll = Random.Shared.Next(1, 101);
        var successLimit = options.Value.SuccessRate;
        var temporaryLimit = successLimit + options.Value.TemporaryFailureRate;
        var permanentLimit = temporaryLimit + options.Value.PermanentFailureRate;

        if (roll <= successLimit)
        {
            return FakePaymentProviderStatus.Approved;
        }

        if (roll <= temporaryLimit)
        {
            return FakePaymentProviderStatus.TemporaryFailure;
        }

        if (roll <= permanentLimit)
        {
            return FakePaymentProviderStatus.Rejected;
        }

        return FakePaymentProviderStatus.Timeout;
    }
}

public sealed class AlwaysApprovePaymentProviderStrategy : IPaymentProviderStrategy
{
    public FakePaymentProviderStatus Choose(FakePaymentProviderRequest request) => FakePaymentProviderStatus.Approved;
}

public sealed class AlwaysTemporaryFailPaymentProviderStrategy : IPaymentProviderStrategy
{
    public FakePaymentProviderStatus Choose(FakePaymentProviderRequest request) => FakePaymentProviderStatus.TemporaryFailure;
}

public sealed class AlwaysRejectPaymentProviderStrategy : IPaymentProviderStrategy
{
    public FakePaymentProviderStatus Choose(FakePaymentProviderRequest request) => FakePaymentProviderStatus.Rejected;
}

public sealed class TimeoutPaymentProviderStrategy : IPaymentProviderStrategy
{
    public FakePaymentProviderStatus Choose(FakePaymentProviderRequest request) => FakePaymentProviderStatus.Timeout;
}
