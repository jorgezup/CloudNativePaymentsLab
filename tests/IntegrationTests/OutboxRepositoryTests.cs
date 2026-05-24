using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;
using FluentAssertions;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class OutboxRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task LockNextBatchAsync_WhenPendingMessagesExist_ShouldReturnEligibleMessages()
    {
        // Arrange
        await using var dbContext = fixture.CreateDbContext();
        var repository = new OutboxRepository(dbContext);
        var pending = CreateMessage("OrderCreated", DateTimeOffset.UtcNow.AddMinutes(-2));
        var published = CreateMessage("OrderCreated", DateTimeOffset.UtcNow.AddMinutes(-1));
        published.MarkAsPublished(DateTimeOffset.UtcNow);

        await repository.AddAsync(pending, CancellationToken.None);
        await repository.AddAsync(published, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        var messages = await repository.LockNextBatchAsync(10, 3, CancellationToken.None);

        // Assert
        messages.Should().ContainSingle(message => message.Id == pending.Id);
        messages.Should().NotContain(message => message.Id == published.Id);
    }

    private static OutboxMessage CreateMessage(string eventType, DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Order",
            eventType,
            "{}",
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAt);
}
