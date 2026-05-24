namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public sealed record CreateOrderRequest(
    string CustomerId,
    decimal Amount,
    string Currency,
    string? ExternalReference);
