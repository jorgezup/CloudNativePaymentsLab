using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;

public sealed class PaymentRepository(PaymentsDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Payments.FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Payments.FirstOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

    public Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.Payments.FirstOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetDueRetriesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
        await dbContext.Payments
            .Where(payment => payment.Status == PaymentStatus.RetryScheduled && payment.NextRetryAt <= now)
            .OrderBy(payment => payment.NextRetryAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        await dbContext.Payments.AddAsync(payment, cancellationToken);
}
