using CloudNativePaymentsLab.Api.Modules.Orders.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public sealed record OrderResponse(
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    string Currency,
    OrderStatus Status,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
