using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public static class MessagingDebugEndpoints
{
    public static IEndpointRouteBuilder MapMessagingDebugEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/outbox", async (PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var messages = await dbContext.OutboxMessages
                .AsNoTracking()
                .OrderByDescending(message => message.CreatedAt)
                .Take(50)
                .Select(message => new
                {
                    message.Id,
                    message.AggregateId,
                    message.AggregateType,
                    message.EventType,
                    message.Status,
                    message.RetryCount,
                    message.CreatedAt,
                    message.PublishedAt,
                    message.LastError,
                    message.CorrelationId,
                    message.CausationId
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(messages);
        });

        endpoints.MapGet("/inbox", async (PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var messages = await dbContext.InboxMessages
                .AsNoTracking()
                .OrderByDescending(message => message.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            return Results.Ok(messages);
        });

        return endpoints;
    }
}
