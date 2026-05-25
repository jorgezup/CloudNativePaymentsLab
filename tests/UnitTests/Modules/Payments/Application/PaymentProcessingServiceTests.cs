using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Application;

public sealed class PaymentProcessingServiceTests
{
    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenMessageWasAlreadyProcessed_ShouldIgnoreWithoutCallingProvider()
    {
        await using var dbContext = CreateDbContext();
        var envelope = CreateEnvelope(Guid.NewGuid());
        await dbContext.InboxMessages.AddAsync(new InboxMessage(envelope.MessageId, PaymentProcessingService.OrderCreatedConsumerName, envelope.EventType, envelope.AggregateId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        var provider = new FakePaymentProviderClient(ApprovedResponse());
        var service = CreateService(dbContext, provider);

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        provider.CallCount.Should().Be(0);
        dbContext.Payments.Should().BeEmpty();
        dbContext.PaymentAttempts.Should().BeEmpty();
        dbContext.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenOrderDoesNotExist_ShouldMoveMessageToDeadLetter()
    {
        await using var dbContext = CreateDbContext();
        var envelope = CreateEnvelope(Guid.NewGuid());
        var service = CreateService(dbContext, new FakePaymentProviderClient(ApprovedResponse()));

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        dbContext.DeadLetterMessages.Should().ContainSingle(message =>
            message.OriginalMessageId == envelope.MessageId &&
            message.ErrorMessage == "Order was not found");
        dbContext.InboxMessages.Should().ContainSingle(message => message.MessageId == envelope.MessageId);
    }

    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenProviderApproves_ShouldApprovePaymentAndCreateOutboxEvent()
    {
        await using var dbContext = CreateDbContext();
        var order = await SeedOrderAsync(dbContext);
        var envelope = CreateEnvelope(order.Id);
        var service = CreateService(dbContext, new FakePaymentProviderClient(ApprovedResponse()));

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        dbContext.Payments.Should().ContainSingle(payment =>
            payment.OrderId == order.Id &&
            payment.Status == PaymentStatus.Approved &&
            payment.ProviderTransactionId == "provider-123");
        dbContext.Orders.Single().Status.Should().Be(OrderStatus.Paid);
        dbContext.PaymentAttempts.Should().ContainSingle(attempt => attempt.Status == PaymentAttemptStatus.Approved);
        dbContext.InboxMessages.Should().ContainSingle(message => message.MessageId == envelope.MessageId);
        dbContext.OutboxMessages.Should().ContainSingle(message =>
            message.EventType == "PaymentApproved" &&
            message.Topic == "payments.payment-approved");
    }

    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenProviderRejects_ShouldFailPaymentAndCreateOutboxEvent()
    {
        await using var dbContext = CreateDbContext();
        var order = await SeedOrderAsync(dbContext);
        var envelope = CreateEnvelope(order.Id);
        var service = CreateService(dbContext, new FakePaymentProviderClient(RejectedResponse()));

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        dbContext.Payments.Should().ContainSingle(payment => payment.Status == PaymentStatus.Failed);
        dbContext.Orders.Single().Status.Should().Be(OrderStatus.PaymentFailed);
        dbContext.PaymentAttempts.Should().ContainSingle(attempt => attempt.Status == PaymentAttemptStatus.PermanentError);
        dbContext.OutboxMessages.Should().ContainSingle(message =>
            message.EventType == "PaymentFailed" &&
            message.Topic == "payments.payment-failed");
    }

    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenProviderTemporarilyFails_ShouldScheduleRetryWithoutTerminalOutboxEvent()
    {
        await using var dbContext = CreateDbContext();
        var order = await SeedOrderAsync(dbContext);
        var envelope = CreateEnvelope(order.Id);
        var service = CreateService(dbContext, new FakePaymentProviderClient(TemporaryFailureResponse()));

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        dbContext.Payments.Should().ContainSingle(payment =>
            payment.Status == PaymentStatus.RetryScheduled &&
            payment.NextRetryAt != null);
        dbContext.Orders.Single().Status.Should().Be(OrderStatus.PaymentPendingRetry);
        dbContext.PaymentAttempts.Should().ContainSingle(attempt => attempt.Status == PaymentAttemptStatus.RetryableError);
        dbContext.InboxMessages.Should().ContainSingle(message => message.MessageId == envelope.MessageId);
        dbContext.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessOrderCreatedAsync_WhenPaymentAlreadyApproved_ShouldSaveInboxOnly()
    {
        await using var dbContext = CreateDbContext();
        var order = await SeedOrderAsync(dbContext);
        var envelope = CreateEnvelope(order.Id);
        var payment = Payment.Create(order.Id, order.CustomerId, order.Amount, order.Currency, $"PAYMENT:{order.Id}", envelope.MessageId, "orders.order-created", "OrderCreated", DateTimeOffset.UtcNow);
        payment.MarkApproved("provider-123", DateTimeOffset.UtcNow);
        await dbContext.Payments.AddAsync(payment);
        await dbContext.SaveChangesAsync();
        var provider = new FakePaymentProviderClient(ApprovedResponse());
        var service = CreateService(dbContext, provider);

        await service.ProcessOrderCreatedAsync(envelope, "{}", CancellationToken.None);

        provider.CallCount.Should().Be(0);
        dbContext.InboxMessages.Should().ContainSingle(message => message.MessageId == envelope.MessageId);
        dbContext.PaymentAttempts.Should().BeEmpty();
        dbContext.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessRetryAsync_WhenMaxAttemptsExceeded_ShouldMovePaymentToDeadLetter()
    {
        await using var dbContext = CreateDbContext();
        var order = await SeedOrderAsync(dbContext);
        var payment = Payment.Create(order.Id, order.CustomerId, order.Amount, order.Currency, $"PAYMENT:{order.Id}", Guid.NewGuid(), "orders.order-created", "OrderCreated", DateTimeOffset.UtcNow);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            payment.MarkAttemptStarted(DateTimeOffset.UtcNow);
        }

        payment.ScheduleRetry("Temporary provider instability", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow);
        await dbContext.Payments.AddAsync(payment);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new FakePaymentProviderClient(TemporaryFailureResponse()));

        await service.ProcessRetryAsync(payment.Id, CancellationToken.None);

        dbContext.Payments.Single().Status.Should().Be(PaymentStatus.DeadLettered);
        dbContext.Orders.Single().Status.Should().Be(OrderStatus.PaymentFailed);
        dbContext.DeadLetterMessages.Should().ContainSingle(message => message.OriginalMessageId == payment.OriginalMessageId);
        dbContext.OutboxMessages.Should().ContainSingle(message =>
            message.EventType == "PaymentMovedToDeadLetter" &&
            message.Topic == "payments.dlq");
    }

    [Fact]
    public async Task MoveInvalidMessageToDeadLetterAsync_WhenCalledTwice_ShouldCreateSingleDeadLetter()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakePaymentProviderClient(ApprovedResponse()));

        await service.MoveInvalidMessageToDeadLetterAsync("{ invalid", "Invalid JSON", CancellationToken.None);
        await service.MoveInvalidMessageToDeadLetterAsync("{ invalid", "Invalid JSON", CancellationToken.None);

        dbContext.DeadLetterMessages.Should().ContainSingle();
        dbContext.InboxMessages.Should().ContainSingle();
    }

    private static PaymentProcessingService CreateService(PaymentsDbContext dbContext, FakePaymentProviderClient providerClient) =>
        new(
            dbContext,
            new OrderRepository(dbContext),
            new PaymentRepository(dbContext),
            new PaymentAttemptRepository(dbContext),
            new DeadLetterRepository(dbContext),
            new InboxRepository(dbContext),
            new OutboxRepository(dbContext),
            new PaymentIdempotencyKeyStrategy(),
            providerClient,
            new IntegrationEventBuilder(Options.Create(new KafkaOptions())),
            Options.Create(new PaymentsOptions { MaxProcessingAttempts = 5, RetryDelaySeconds = 5 }),
            Options.Create(new KafkaOptions()),
            NullLogger<PaymentProcessingService>.Instance);

    private static PaymentsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PaymentsDbContext(options);
    }

    private static async Task<Order> SeedOrderAsync(PaymentsDbContext dbContext)
    {
        var order = Order.Create("customer-1", 199.90m, "BRL", "ORDER:customer-1:reference-1", DateTimeOffset.UtcNow);
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();
        return order;
    }

    private static IntegrationEventEnvelope<OrderCreatedPayload> CreateEnvelope(Guid orderId)
    {
        var messageId = Guid.NewGuid();
        return new IntegrationEventEnvelope<OrderCreatedPayload>(
            messageId,
            "OrderCreated",
            orderId,
            "Order",
            messageId,
            messageId,
            DateTimeOffset.UtcNow,
            new OrderCreatedPayload(orderId, "customer-1", 199.90m, "BRL", $"ORDER:customer-1:{orderId}"));
    }

    private static FakePaymentProviderResponse ApprovedResponse() =>
        new("provider-123", FakePaymentProviderStatus.Approved, "00", "Payment approved");

    private static FakePaymentProviderResponse RejectedResponse() =>
        new(null, FakePaymentProviderStatus.Rejected, "CARD_DECLINED", "Payment rejected");

    private static FakePaymentProviderResponse TemporaryFailureResponse() =>
        new(null, FakePaymentProviderStatus.TemporaryFailure, "TEMP_001", "Temporary provider instability");

    private sealed class FakePaymentProviderClient(params FakePaymentProviderResponse[] responses) : IPaymentProviderClient
    {
        private readonly Queue<FakePaymentProviderResponse> responses = new(responses);

        public int CallCount { get; private set; }

        public Task<FakePaymentProviderResponse> PayAsync(FakePaymentProviderRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responses.Count > 0 ? responses.Dequeue() : ApprovedResponse());
        }
    }
}
