namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public sealed class PaymentIdempotencyKeyStrategy : IPaymentIdempotencyKeyStrategy
{
    public string Generate(Guid orderId) => $"PAYMENT:{orderId}";
}
