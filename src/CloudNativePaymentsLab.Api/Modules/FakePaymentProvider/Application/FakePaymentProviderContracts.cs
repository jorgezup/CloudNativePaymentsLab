namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

public sealed record FakePaymentProviderRequest(
    string IdempotencyKey,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string? ForceResult);

public sealed record FakePaymentProviderResponse(
    string? ProviderTransactionId,
    FakePaymentProviderStatus Status,
    string ResponseCode,
    string Message);

public enum FakePaymentProviderStatus
{
    Approved,
    TemporaryFailure,
    Rejected,
    Timeout
}
