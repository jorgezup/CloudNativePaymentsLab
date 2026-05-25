using System.Net.Http.Json;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public interface IPaymentProviderClient
{
    Task<FakePaymentProviderResponse> PayAsync(FakePaymentProviderRequest request, CancellationToken cancellationToken);
}

public sealed class PaymentProviderClient(HttpClient httpClient) : IPaymentProviderClient
{
    public async Task<FakePaymentProviderResponse> PayAsync(FakePaymentProviderRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/fake-provider/payments", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FakePaymentProviderResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Fake payment provider returned an empty response");
    }
}
