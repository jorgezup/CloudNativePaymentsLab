using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Presentation;

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/payments", async (PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var payments = await dbContext.Payments
                .AsNoTracking()
                .OrderByDescending(payment => payment.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            return Results.Ok(payments);
        });

        endpoints.MapGet("/payments/{id:guid}", async (Guid id, PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var payment = await dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });

        endpoints.MapGet("/orders/{orderId:guid}/payment", async (Guid orderId, PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var payment = await dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });

        endpoints.MapGet("/payment-attempts", async (PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var attempts = await dbContext.PaymentAttempts
                .AsNoTracking()
                .OrderByDescending(attempt => attempt.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Results.Ok(attempts);
        });

        endpoints.MapGet("/orders/{orderId:guid}/payment-attempts", async (Guid orderId, PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var attempts = await dbContext.PaymentAttempts
                .AsNoTracking()
                .Where(attempt => attempt.OrderId == orderId)
                .OrderBy(attempt => attempt.AttemptNumber)
                .ToListAsync(cancellationToken);

            return Results.Ok(attempts);
        });

        endpoints.MapGet("/dead-letter", async (PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var messages = await dbContext.DeadLetterMessages
                .AsNoTracking()
                .OrderByDescending(message => message.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            return Results.Ok(messages);
        });

        endpoints.MapGet("/dead-letter/{id:guid}", async (Guid id, PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var message = await dbContext.DeadLetterMessages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return message is null ? Results.NotFound() : Results.Ok(message);
        });

        return endpoints;
    }
}
