using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Infrastructure;
using FluentAssertions;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class OrderRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Repository_WhenOrderIsSaved_ShouldBePersistedInPostgreSql()
    {
        // Arrange
        await using var dbContext = fixture.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create("customer-1", 42.50m, "BRL", $"ORDER:customer-1:{Guid.NewGuid():N}", now);

        // Act
        await repository.AddAsync(order, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var persisted = await repository.GetByIdAsync(order.Id, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(order.Id);
        persisted.CustomerId.Should().Be("customer-1");
        persisted.Amount.Should().Be(42.50m);
        persisted.Currency.Should().Be("BRL");
        persisted.Status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public async Task Repository_WhenIdempotencyKeyExists_ShouldReturnOrder()
    {
        // Arrange
        await using var dbContext = fixture.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        var idempotencyKey = $"ORDER:customer-2:{Guid.NewGuid():N}";
        var order = Order.Create("customer-2", 99.90m, "USD", idempotencyKey, DateTimeOffset.UtcNow);
        await repository.AddAsync(order, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        var persisted = await repository.GetByIdempotencyKeyAsync(idempotencyKey, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.IdempotencyKey.Should().Be(idempotencyKey);
    }
}
