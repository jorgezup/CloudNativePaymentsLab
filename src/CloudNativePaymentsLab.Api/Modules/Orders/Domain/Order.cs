namespace CloudNativePaymentsLab.Api.Modules.Orders.Domain;

public sealed class Order
{
    private Order()
    {
    }

    private Order(Guid id, string customerId, decimal amount, string currency, string idempotencyKey, DateTimeOffset now)
    {
        Id = id;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency;
        Status = OrderStatus.Created;
        IdempotencyKey = idempotencyKey;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order Create(string customerId, decimal amount, string currency, string idempotencyKey, DateTimeOffset now) =>
        new(Guid.NewGuid(), customerId, amount, currency.ToUpperInvariant(), idempotencyKey, now);

    public void MarkAsProcessing(DateTimeOffset now)
    {
        if (Status is OrderStatus.Created)
        {
            Status = OrderStatus.Processing;
            UpdatedAt = now;
        }
    }

    public void MarkAsPaid(DateTimeOffset now)
    {
        if (Status is not OrderStatus.Paid)
        {
            Status = OrderStatus.Paid;
            UpdatedAt = now;
        }
    }

    public void MarkAsPaymentFailed(DateTimeOffset now)
    {
        if (Status is not OrderStatus.PaymentFailed)
        {
            Status = OrderStatus.PaymentFailed;
            UpdatedAt = now;
        }
    }

    public void MarkAsPaymentPendingRetry(DateTimeOffset now)
    {
        if (Status is not OrderStatus.PaymentPendingRetry)
        {
            Status = OrderStatus.PaymentPendingRetry;
            UpdatedAt = now;
        }
    }
}
