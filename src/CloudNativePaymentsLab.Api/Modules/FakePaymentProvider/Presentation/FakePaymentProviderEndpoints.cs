using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Presentation;

public static class FakePaymentProviderEndpoints
{
    public static IEndpointRouteBuilder MapFakePaymentProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/fake-provider/payments", async (
            FakePaymentProviderRequest request,
            FakePaymentProviderService provider,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await provider.PayAsync(request, cancellationToken);
                return Results.Ok(response);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        return endpoints;
    }
}
