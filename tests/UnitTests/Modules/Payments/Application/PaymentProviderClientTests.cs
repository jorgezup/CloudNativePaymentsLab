using System.Net;
using System.Text;
using System.Text.Json;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using FluentAssertions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Application;

public sealed class PaymentProviderClientTests
{
    [Fact]
    public async Task PayAsync_WhenProviderApproves_ShouldPostRequestAndReturnResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new FakePaymentProviderResponse("provider-123", FakePaymentProviderStatus.Approved, "00", "Payment approved"))
        });
        var client = new PaymentProviderClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8081")
        });
        var request = new FakePaymentProviderRequest("PAYMENT:order-1", Guid.NewGuid(), 199.90m, "BRL", null);

        var response = await client.PayAsync(request, CancellationToken.None);

        response.Status.Should().Be(FakePaymentProviderStatus.Approved);
        response.ProviderTransactionId.Should().Be("provider-123");
        handler.Requests.Should().ContainSingle();
        handler.Requests.Single().Method.Should().Be(HttpMethod.Post);
        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be("/fake-provider/payments");
    }

    [Fact]
    public async Task PayAsync_WhenProviderReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new PaymentProviderClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8081")
        });
        var request = new FakePaymentProviderRequest("PAYMENT:order-1", Guid.NewGuid(), 199.90m, "BRL", null);

        Func<Task> act = () => client.PayAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PayAsync_WhenProviderReturnsEmptyJson_ShouldThrowInvalidOperationException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });
        var client = new PaymentProviderClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8081")
        });
        var request = new FakePaymentProviderRequest("PAYMENT:order-1", Guid.NewGuid(), 199.90m, "BRL", null);

        Func<Task> act = () => client.PayAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fake payment provider returned an empty response");
    }

    private static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
