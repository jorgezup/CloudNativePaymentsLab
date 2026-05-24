using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;
using FluentAssertions;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class InboxRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ExistsAsync_WhenMessageWasSaved_ShouldReturnTrue()
    {
        // Arrange
        await using var dbContext = fixture.CreateDbContext();
        var repository = new InboxRepository(dbContext);
        var message = new InboxMessage(Guid.NewGuid(), "orders-consumer", "OrderCreated", Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Act
        await repository.AddAsync(message, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var exists = await repository.ExistsAsync(message.MessageId, message.ConsumerName, CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenMessageWasNotSaved_ShouldReturnFalse()
    {
        // Arrange
        await using var dbContext = fixture.CreateDbContext();
        var repository = new InboxRepository(dbContext);

        // Act
        var exists = await repository.ExistsAsync(Guid.NewGuid(), "orders-consumer", CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }
}
