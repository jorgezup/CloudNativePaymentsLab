using CloudNativePaymentsLab.Api.Modules.Payments.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public interface IDeadLetterRepository
{
    Task<bool> ExistsAsync(Guid originalMessageId, string consumerName, CancellationToken cancellationToken);
    Task AddAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}
