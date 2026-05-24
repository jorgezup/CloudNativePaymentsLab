using CloudNativePaymentsLab.Api.Modules.Orders.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
}
