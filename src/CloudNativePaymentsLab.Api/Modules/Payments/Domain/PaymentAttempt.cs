namespace CloudNativePaymentsLab.Api.Modules.Payments.Domain;

public sealed class PaymentAttempt
{
    private PaymentAttempt()
    {
    }

    private PaymentAttempt(Guid id, Guid paymentId, Guid orderId, int attemptNumber, string idempotencyKey, DateTimeOffset now)
    {
        Id = id;
        PaymentId = paymentId;
        OrderId = orderId;
        AttemptNumber = attemptNumber;
        Status = PaymentAttemptStatus.Started;
        IdempotencyKey = idempotencyKey;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    public int AttemptNumber { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ProviderTransactionId { get; private set; }
    public string? ProviderResponseCode { get; private set; }
    public string? ProviderResponseMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public static PaymentAttempt Start(Guid paymentId, Guid orderId, int attemptNumber, string idempotencyKey, DateTimeOffset now) =>
        new(Guid.NewGuid(), paymentId, orderId, attemptNumber, idempotencyKey, now);

    public void MarkApproved(string providerTransactionId, string responseCode, string responseMessage, DateTimeOffset now) =>
        Finish(PaymentAttemptStatus.Approved, providerTransactionId, responseCode, responseMessage, null, now);

    public void MarkPermanentError(string responseCode, string responseMessage, DateTimeOffset now) =>
        Finish(PaymentAttemptStatus.PermanentError, null, responseCode, responseMessage, responseMessage, now);

    public void MarkRetryableError(string responseCode, string responseMessage, DateTimeOffset now) =>
        Finish(PaymentAttemptStatus.RetryableError, null, responseCode, responseMessage, responseMessage, now);

    public void MarkTimeout(string errorMessage, DateTimeOffset now) =>
        Finish(PaymentAttemptStatus.Timeout, null, "TIMEOUT", errorMessage, errorMessage, now);

    private void Finish(
        PaymentAttemptStatus status,
        string? providerTransactionId,
        string? providerResponseCode,
        string? providerResponseMessage,
        string? errorMessage,
        DateTimeOffset now)
    {
        Status = status;
        ProviderTransactionId = providerTransactionId;
        ProviderResponseCode = providerResponseCode;
        ProviderResponseMessage = providerResponseMessage;
        ErrorMessage = errorMessage is { Length: > 2000 } ? errorMessage[..2000] : errorMessage;
        FinishedAt = now;
    }
}
