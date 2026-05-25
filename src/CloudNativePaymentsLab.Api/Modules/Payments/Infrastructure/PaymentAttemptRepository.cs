using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;

public sealed class PaymentAttemptRepository(PaymentsDbContext dbContext) : IPaymentAttemptRepository
{
    public async Task AddAsync(PaymentAttempt attempt, CancellationToken cancellationToken) =>
        await dbContext.PaymentAttempts.AddAsync(attempt, cancellationToken);
}
