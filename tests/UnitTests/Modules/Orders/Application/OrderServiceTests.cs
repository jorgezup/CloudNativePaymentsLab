using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudNativePaymentsLab.UnitTests.Modules.Orders.Application;

public sealed class OrderServiceTests
{
    [Theory]
    [InlineData("", 10, "BRL", "customerId is required")]
    [InlineData("customer-1", 0, "BRL", "amount must be greater than zero")]
    [InlineData("customer-1", 10, "B", "currency must use a 3-letter ISO code")]
    public async Task CreateAsync_WhenRequestIsInvalid_ShouldThrowArgumentException(
        string customerId,
        decimal amount,
        string currency,
        string expectedMessage)
    {
        // Arrange
        var service = CreateService(new FakeOrderRepository());
        var request = new CreateOrderRequest(customerId, amount, currency, "reference-1");

        // Act
        Func<Task> act = () => service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedMessage);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderExists_ShouldReturnResponse()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var order = Order.Create("customer-1", 100, "BRL", "ORDER:customer-1:reference-1", createdAt);
        var repository = new FakeOrderRepository(order);
        var service = CreateService(repository);

        // Act
        var response = await service.GetByIdAsync(order.Id, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response!.OrderId.Should().Be(order.Id);
        response.CustomerId.Should().Be("customer-1");
        response.Amount.Should().Be(100);
        response.Currency.Should().Be("BRL");
        response.IdempotencyKey.Should().Be("ORDER:customer-1:reference-1");
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var service = CreateService(new FakeOrderRepository());

        // Act
        var response = await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenOrderAlreadyExists_ShouldReturnExistingResponse()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var order = Order.Create("customer-1", 100, "BRL", "ORDER:customer-1:reference-1", createdAt);
        var repository = new FakeOrderRepository(order);
        var service = CreateService(repository);
        var request = new CreateOrderRequest("customer-1", 100, "BRL", "reference-1");

        // Act
        var response = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        response.OrderId.Should().Be(order.Id);
        response.IdempotencyKey.Should().Be(order.IdempotencyKey);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ShouldCreateOrderAndOutboxMessage()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var orderRepository = new FakeOrderRepository();
        var outboxRepository = new FakeOutboxRepository();
        var service = CreateService(orderRepository, outboxRepository, dbContext);
        var request = new CreateOrderRequest(" customer-1 ", 100, " BRL ", "reference-1");

        // Act
        var response = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        response.CustomerId.Should().Be("customer-1");
        response.Currency.Should().Be("BRL");
        response.IdempotencyKey.Should().Be("ORDER:customer-1:reference-1");
        orderRepository.Orders.Should().ContainSingle(order => order.Id == response.OrderId);
        outboxRepository.Messages.Should().ContainSingle(message => message.AggregateId == response.OrderId);
    }

    private static OrderService CreateService(
        FakeOrderRepository orderRepository,
        FakeOutboxRepository? outboxRepository = null,
        PaymentsDbContext? dbContext = null) =>
        new(
            dbContext ?? null!,
            orderRepository,
            outboxRepository ?? new FakeOutboxRepository(),
            new OrderIdempotencyKeyStrategy(),
            new IntegrationEventBuilder(),
            NullLogger<OrderService>.Instance);

    private static PaymentsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PaymentsDbContext(options);
    }

    private sealed class FakeOrderRepository(params Order[] orders) : IOrderRepository
    {
        private readonly List<Order> orders = [.. orders];

        public IReadOnlyList<Order> Orders => orders;

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(orders.FirstOrDefault(order => order.Id == id));

        public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(orders.FirstOrDefault(order => order.IdempotencyKey == idempotencyKey));

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            orders.Add(order);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        private readonly List<OutboxMessage> messages = [];

        public IReadOnlyList<OutboxMessage> Messages => messages;

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
    }
}
