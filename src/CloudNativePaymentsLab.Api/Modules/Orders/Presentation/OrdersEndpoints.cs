using CloudNativePaymentsLab.Api.Modules.Orders.Application;

namespace CloudNativePaymentsLab.Api.Modules.Orders.Presentation;

public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/orders", async (
            CreateOrderRequest request,
            OrderService orderService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var order = await orderService.CreateAsync(request, cancellationToken);
                return Results.Ok(new CreateOrderResponse(order.OrderId, order.Status, order.IdempotencyKey));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        endpoints.MapGet("/orders/{id:guid}", async (
            Guid id,
            OrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var response = await orderService.GetByIdAsync(id, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return endpoints;
    }
}
