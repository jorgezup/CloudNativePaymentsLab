using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public sealed class OrderService(
    PaymentsDbContext dbContext,
    IOrderRepository orderRepository,
    IOutboxRepository outboxRepository,
    IIdempotencyKeyStrategy idempotencyKeyStrategy,
    IntegrationEventBuilder eventBuilder,
    ILogger<OrderService> logger)
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var idempotencyKey = idempotencyKeyStrategy.Generate(request.CustomerId, request.ExternalReference);
        var existing = await orderRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Order idempotency hit for key {IdempotencyKey}. Returning order {OrderId}", idempotencyKey, existing.Id);
            return ToResponse(existing);
        }

        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(request.CustomerId.Trim(), request.Amount, request.Currency.Trim(), idempotencyKey, now);
        var outboxMessage = eventBuilder.BuildOrderCreated(order, now);

        // O DbContext do EF Core atua como Unit of Work nesta POC. Pedido e Outbox sao gravados
        // na mesma transacao para que o banco nunca tenha um pedido sem o evento que precisa ser publicado.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await orderRepository.AddAsync(order, cancellationToken);
            await outboxRepository.AddAsync(outboxMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            existing = await orderRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                logger.LogInformation("Order idempotency race resolved for key {IdempotencyKey}. Returning order {OrderId}", idempotencyKey, existing.Id);
                return ToResponse(existing);
            }

            throw;
        }

        logger.LogInformation("Order created {OrderId}", order.Id);
        logger.LogInformation("Outbox message saved {OutboxMessageId} for order {OrderId}", outboxMessage.Id, order.Id);

        return ToResponse(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(id, cancellationToken);
        return order is null ? null : ToResponse(order);
    }

    private static OrderResponse ToResponse(Order order) =>
        new(order.Id, order.CustomerId, order.Amount, order.Currency, order.Status, order.IdempotencyKey, order.CreatedAt, order.UpdatedAt);

    private static void Validate(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("customerId is required");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("amount must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            throw new ArgumentException("currency must use a 3-letter ISO code");
        }
    }
}
