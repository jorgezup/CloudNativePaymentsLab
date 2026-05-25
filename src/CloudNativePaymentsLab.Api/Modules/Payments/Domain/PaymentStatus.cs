namespace CloudNativePaymentsLab.Api.Modules.Payments.Domain;

public enum PaymentStatus
{
    Pending,
    Approved,
    Failed,
    RetryScheduled,
    DeadLettered
}
