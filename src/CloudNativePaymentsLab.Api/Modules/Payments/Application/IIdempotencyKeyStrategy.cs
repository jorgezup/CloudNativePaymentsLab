namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public interface IPaymentIdempotencyKeyStrategy
{
    string Generate(Guid orderId);
}
