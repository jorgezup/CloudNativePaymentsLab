using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken);
}
