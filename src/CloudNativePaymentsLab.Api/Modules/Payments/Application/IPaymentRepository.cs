using CloudNativePaymentsLab.Api.Modules.Payments.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<Payment>> GetDueRetriesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
