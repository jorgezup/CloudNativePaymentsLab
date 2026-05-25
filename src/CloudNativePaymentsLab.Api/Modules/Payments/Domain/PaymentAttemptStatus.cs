namespace CloudNativePaymentsLab.Api.Modules.Payments.Domain;

public enum PaymentAttemptStatus
{
    Started,
    Approved,
    Failed,
    Timeout,
    RetryableError,
    PermanentError
}
