using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class PaymentRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task GetByIdempotencyKeyAsync_WhenPaymentExists_ShouldReturnPayment()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentRepository(dbContext);
        var payment = CreatePayment();

        await repository.AddAsync(payment, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var found = await repository.GetByIdempotencyKeyAsync(payment.IdempotencyKey, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentExists_ShouldReturnPayment()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentRepository(dbContext);
        var payment = CreatePayment();

        await repository.AddAsync(payment, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var found = await repository.GetByIdAsync(payment.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.OrderId.Should().Be(payment.OrderId);
    }

    [Fact]
    public async Task GetByOrderIdAsync_WhenPaymentExists_ShouldReturnPayment()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentRepository(dbContext);
        var payment = CreatePayment();

        await repository.AddAsync(payment, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var found = await repository.GetByOrderIdAsync(payment.OrderId, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetDueRetriesAsync_WhenRetryIsDue_ShouldReturnPayment()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentRepository(dbContext);
        var now = DateTimeOffset.UtcNow;
        var payment = CreatePayment();
        payment.MarkAttemptStarted(now);
        payment.ScheduleRetry("Temporary provider instability", now.AddSeconds(-1), now);

        await repository.AddAsync(payment, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var duePayments = await repository.GetDueRetriesAsync(now, 10, CancellationToken.None);

        duePayments.Should().Contain(item => item.Id == payment.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenIdempotencyKeyIsDuplicated_ShouldFail()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentRepository(dbContext);
        var payment = CreatePayment();
        var duplicate = Payment.Create(
            Guid.NewGuid(),
            "customer-2",
            250m,
            "BRL",
            payment.IdempotencyKey,
            Guid.NewGuid(),
            "orders.order-created",
            "OrderCreated",
            DateTimeOffset.UtcNow);

        await repository.AddAsync(payment, CancellationToken.None);
        await repository.AddAsync(duplicate, CancellationToken.None);

        Func<Task> act = () => dbContext.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static Payment CreatePayment()
    {
        var orderId = Guid.NewGuid();
        return Payment.Create(
            orderId,
            "customer-1",
            199.90m,
            "BRL",
            $"PAYMENT:{orderId}",
            Guid.NewGuid(),
            "orders.order-created",
            "OrderCreated",
            DateTimeOffset.UtcNow);
    }
}
