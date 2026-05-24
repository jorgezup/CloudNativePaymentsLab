using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public interface IInboxRepository
{
    Task<bool> ExistsAsync(Guid messageId, string consumerName, CancellationToken cancellationToken);
    Task AddAsync(InboxMessage message, CancellationToken cancellationToken);
}
