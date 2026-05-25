using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;

public sealed class DeadLetterRepository(PaymentsDbContext dbContext) : IDeadLetterRepository
{
    public Task<bool> ExistsAsync(Guid originalMessageId, string consumerName, CancellationToken cancellationToken) =>
        dbContext.DeadLetterMessages.AnyAsync(
            message => message.OriginalMessageId == originalMessageId && message.ConsumerName == consumerName,
            cancellationToken);

    public async Task AddAsync(DeadLetterMessage message, CancellationToken cancellationToken) =>
        await dbContext.DeadLetterMessages.AddAsync(message, cancellationToken);
}
