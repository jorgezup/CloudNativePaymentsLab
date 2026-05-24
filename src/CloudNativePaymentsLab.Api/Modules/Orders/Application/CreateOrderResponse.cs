using CloudNativePaymentsLab.Api.Modules.Orders.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public sealed record CreateOrderResponse(Guid OrderId, OrderStatus Status, string IdempotencyKey);
