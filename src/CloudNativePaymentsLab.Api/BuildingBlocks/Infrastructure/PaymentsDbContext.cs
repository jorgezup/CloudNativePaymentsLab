using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<DeadLetterMessage> DeadLetterMessages => Set<DeadLetterMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var orderStatusConverter = new EnumToStringConverter<OrderStatus>();
        var outboxStatusConverter = new EnumToStringConverter<OutboxMessageStatus>();
        var paymentStatusConverter = new EnumToStringConverter<PaymentStatus>();
        var paymentAttemptStatusConverter = new EnumToStringConverter<PaymentAttemptStatus>();

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
            entity.Property(message => message.Topic).HasMaxLength(160).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Status).HasConversion(outboxStatusConverter).HasMaxLength(32).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => message.Status);
            entity.HasIndex(message => message.CreatedAt);
            entity.HasIndex(message => new { message.Status, message.RetryCount, message.CreatedAt });
            entity.HasIndex(message => message.AggregateId);
            entity.HasIndex(message => message.CorrelationId);
            entity.HasIndex(message => message.Topic);
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

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.CustomerId).HasMaxLength(120).IsRequired();
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Status).HasConversion(paymentStatusConverter).HasMaxLength(32).IsRequired();
            entity.Property(payment => payment.IdempotencyKey).HasMaxLength(300).IsRequired();
            entity.Property(payment => payment.ProviderTransactionId).HasMaxLength(160);
            entity.Property(payment => payment.LastError).HasMaxLength(2000);
            entity.Property(payment => payment.OriginalTopic).HasMaxLength(160).IsRequired();
            entity.Property(payment => payment.OriginalEventType).HasMaxLength(120).IsRequired();
            entity.HasIndex(payment => payment.IdempotencyKey).IsUnique();
            entity.HasIndex(payment => payment.OrderId);
            entity.HasIndex(payment => payment.CustomerId);
            entity.HasIndex(payment => payment.Status);
            entity.HasIndex(payment => payment.CreatedAt);
            entity.HasIndex(payment => payment.ProviderTransactionId);
            entity.HasIndex(payment => payment.NextRetryAt);
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("PaymentAttempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Status).HasConversion(paymentAttemptStatusConverter).HasMaxLength(32).IsRequired();
            entity.Property(attempt => attempt.IdempotencyKey).HasMaxLength(300).IsRequired();
            entity.Property(attempt => attempt.ProviderTransactionId).HasMaxLength(160);
            entity.Property(attempt => attempt.ProviderResponseCode).HasMaxLength(80);
            entity.Property(attempt => attempt.ProviderResponseMessage).HasMaxLength(1000);
            entity.Property(attempt => attempt.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(attempt => attempt.PaymentId);
            entity.HasIndex(attempt => attempt.OrderId);
            entity.HasIndex(attempt => attempt.IdempotencyKey);
            entity.HasIndex(attempt => attempt.Status);
            entity.HasIndex(attempt => attempt.CreatedAt);
            entity.HasIndex(attempt => new { attempt.OrderId, attempt.AttemptNumber });
        });

        modelBuilder.Entity<DeadLetterMessage>(entity =>
        {
            entity.ToTable("DeadLetterMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Topic).HasMaxLength(160).IsRequired();
            entity.Property(message => message.ConsumerName).HasMaxLength(120).IsRequired();
            entity.Property(message => message.EventType).HasMaxLength(120).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.ErrorMessage).HasMaxLength(4000).IsRequired();
            entity.HasIndex(message => message.OriginalMessageId);
            entity.HasIndex(message => message.Topic);
            entity.HasIndex(message => message.ConsumerName);
            entity.HasIndex(message => message.EventType);
            entity.HasIndex(message => message.AggregateId);
            entity.HasIndex(message => message.CreatedAt);
            entity.HasIndex(message => new { message.OriginalMessageId, message.ConsumerName }).IsUnique();
        });
    }
}
