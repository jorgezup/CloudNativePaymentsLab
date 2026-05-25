namespace CloudNativePaymentsLab.Api.Modules.Payments.Domain;

public sealed class Payment
{
    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid orderId,
        string customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        Guid originalMessageId,
        string originalTopic,
        string originalEventType,
        DateTimeOffset now)
    {
        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency.ToUpperInvariant();
        Status = PaymentStatus.Pending;
        IdempotencyKey = idempotencyKey;
        OriginalMessageId = originalMessageId;
        OriginalTopic = originalTopic;
        OriginalEventType = originalEventType;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ProviderTransactionId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }
    public Guid OriginalMessageId { get; private set; }
    public string OriginalTopic { get; private set; } = string.Empty;
    public string OriginalEventType { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment Create(
        Guid orderId,
        string customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        Guid originalMessageId,
        string originalTopic,
        string originalEventType,
        DateTimeOffset now) =>
        new(Guid.NewGuid(), orderId, customerId, amount, currency, idempotencyKey, originalMessageId, originalTopic, originalEventType, now);

    public void MarkAttemptStarted(DateTimeOffset now)
    {
        AttemptCount++;
        Status = PaymentStatus.Pending;
        NextRetryAt = null;
        LastError = null;
        UpdatedAt = now;
    }

    public void MarkApproved(string providerTransactionId, DateTimeOffset now)
    {
        Status = PaymentStatus.Approved;
        ProviderTransactionId = providerTransactionId;
        NextRetryAt = null;
        LastError = null;
        UpdatedAt = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Status = PaymentStatus.Failed;
        NextRetryAt = null;
        LastError = Truncate(error, 2000);
        UpdatedAt = now;
    }

    public void ScheduleRetry(string error, DateTimeOffset nextRetryAt, DateTimeOffset now)
    {
        Status = PaymentStatus.RetryScheduled;
        NextRetryAt = nextRetryAt;
        LastError = Truncate(error, 2000);
        UpdatedAt = now;
    }

    public void MarkDeadLettered(string error, DateTimeOffset now)
    {
        Status = PaymentStatus.DeadLettered;
        NextRetryAt = null;
        LastError = Truncate(error, 2000);
        UpdatedAt = now;
    }

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
}
