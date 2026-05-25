using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class DeadLetterRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ExistsAsync_WhenDeadLetterExists_ShouldReturnTrue()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new DeadLetterRepository(dbContext);
        var originalMessageId = Guid.NewGuid();
        var message = new DeadLetterMessage(
            Guid.NewGuid(),
            originalMessageId,
            "orders.order-created",
            "CloudNativePaymentsLab.OrderCreatedConsumer",
            "OrderCreated",
            Guid.NewGuid(),
            "{}",
            "Maximum payment processing attempts exceeded",
            5,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(message, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var exists = await repository.ExistsAsync(originalMessageId, "CloudNativePaymentsLab.OrderCreatedConsumer", CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOriginalMessageAndConsumerAreDuplicated_ShouldFail()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new DeadLetterRepository(dbContext);
        var originalMessageId = Guid.NewGuid();
        var first = CreateMessage(originalMessageId, "CloudNativePaymentsLab.OrderCreatedConsumer");
        var duplicate = CreateMessage(originalMessageId, "CloudNativePaymentsLab.OrderCreatedConsumer");

        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(duplicate, CancellationToken.None);

        Func<Task> act = () => dbContext.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static DeadLetterMessage CreateMessage(Guid originalMessageId, string consumerName) =>
        new(
            Guid.NewGuid(),
            originalMessageId,
            "orders.order-created",
            consumerName,
            "OrderCreated",
            Guid.NewGuid(),
            "{}",
            "Maximum payment processing attempts exceeded",
            5,
            DateTimeOffset.UtcNow);
}
