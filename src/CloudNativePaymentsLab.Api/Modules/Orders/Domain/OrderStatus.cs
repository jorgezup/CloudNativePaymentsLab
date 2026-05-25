namespace CloudNativePaymentsLab.Api.Modules.Orders.Domain;

public enum OrderStatus
{
    Created = 0,
    Processing = 1,
    Paid = 2,
    PaymentFailed = 3,
    PaymentPendingRetry = 4
}
