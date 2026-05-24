using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var orderStatusConverter = new EnumToStringConverter<OrderStatus>();
        var outboxStatusConverter = new EnumToStringConverter<OutboxMessageStatus>();

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.CustomerId).HasMaxLength(120).IsRequired();
            entity.Property(order => order.Amount).HasPrecision(18, 2);
            entity.Property(order => order.Currency).HasMaxLength(3).IsRequired();
            entity.Property(order => order.Status).HasConversion(orderStatusConverter).HasMaxLength(32).IsRequired();
            entity.Property(order => order.IdempotencyKey).HasMaxLength(300).IsRequired();
            entity.HasIndex(order => order.IdempotencyKey).IsUnique();
            entity.HasIndex(order => order.CustomerId);
            entity.HasIndex(order => order.Status);
            entity.HasIndex(order => order.CreatedAt);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.AggregateType).HasMaxLength(80).IsRequired();
            entity.Property(message => message.EventType).HasMaxLength(120).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Status).HasConversion(outboxStatusConverter).HasMaxLength(32).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => message.Status);
            entity.HasIndex(message => message.CreatedAt);
            entity.HasIndex(message => new { message.Status, message.RetryCount, message.CreatedAt });
            entity.HasIndex(message => message.AggregateId);
            entity.HasIndex(message => message.CorrelationId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(message => new { message.MessageId, message.ConsumerName });
            entity.Property(message => message.ConsumerName).HasMaxLength(120).IsRequired();
            entity.Property(message => message.EventType).HasMaxLength(120).IsRequired();
            entity.HasIndex(message => new { message.MessageId, message.ConsumerName }).IsUnique();
            entity.HasIndex(message => message.AggregateId);
            entity.HasIndex(message => message.EventType);
            entity.HasIndex(message => message.ProcessedAt);
        });
    }
}
