using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public sealed class InboxRepository(PaymentsDbContext dbContext) : IInboxRepository
{
    public Task<bool> ExistsAsync(Guid messageId, string consumerName, CancellationToken cancellationToken) =>
        dbContext.InboxMessages.AnyAsync(
            message => message.MessageId == messageId && message.ConsumerName == consumerName,
            cancellationToken);

    public async Task AddAsync(InboxMessage message, CancellationToken cancellationToken) =>
        await dbContext.InboxMessages.AddAsync(message, cancellationToken);
}
