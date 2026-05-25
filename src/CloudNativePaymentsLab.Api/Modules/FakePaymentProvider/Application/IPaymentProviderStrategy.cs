namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

public interface IPaymentProviderStrategy
{
    FakePaymentProviderStatus Choose(FakePaymentProviderRequest request);
}
