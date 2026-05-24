using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public sealed class OutboxRepository(PaymentsDbContext dbContext) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken)
    {
        // FOR UPDATE SKIP LOCKED evita que duas instancias do worker publiquem a mesma mensagem.
        // Os indices em Status/RetryCount/CreatedAt mantem esse polling barato conforme a Outbox cresce.
        return await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM "OutboxMessages"
                WHERE "Status" IN ('Pending', 'Failed')
                  AND "RetryCount" < {maxRetryCount}
                ORDER BY "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
    }
}
