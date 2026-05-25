using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using FluentAssertions;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class PaymentAttemptRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AddAsync_WhenAttemptIsProvided_ShouldPersistAttempt()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PaymentAttemptRepository(dbContext);
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var attempt = PaymentAttempt.Start(paymentId, orderId, 1, $"PAYMENT:{orderId}", DateTimeOffset.UtcNow);
        attempt.MarkApproved("provider-123", "00", "Payment approved", DateTimeOffset.UtcNow);

        await repository.AddAsync(attempt, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        dbContext.PaymentAttempts.Should().ContainSingle(item =>
            item.Id == attempt.Id &&
            item.PaymentId == paymentId &&
            item.OrderId == orderId &&
            item.Status == PaymentAttemptStatus.Approved);
    }
}
