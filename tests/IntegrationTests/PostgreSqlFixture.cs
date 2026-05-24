using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudNativePaymentsLab.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("payments_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public DbContextOptions<PaymentsDbContext> DbContextOptions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        DbContextOptions = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() =>
        await container.DisposeAsync();

    public PaymentsDbContext CreateDbContext() =>
        new(DbContextOptions);
}
