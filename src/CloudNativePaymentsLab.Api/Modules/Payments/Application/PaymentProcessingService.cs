using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using CloudNativePaymentsLab.Api.Modules.Orders.Domain;
using CloudNativePaymentsLab.Api.Modules.Payments.Domain;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public sealed class PaymentProcessingService(
    PaymentsDbContext dbContext,
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IPaymentAttemptRepository paymentAttemptRepository,
    IDeadLetterRepository deadLetterRepository,
    IInboxRepository inboxRepository,
    IOutboxRepository outboxRepository,
    IPaymentIdempotencyKeyStrategy idempotencyKeyStrategy,
    IPaymentProviderClient providerClient,
    IntegrationEventBuilder eventBuilder,
    IOptions<PaymentsOptions> paymentsOptions,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<PaymentProcessingService> logger)
{
    public const string OrderCreatedConsumerName = "CloudNativePaymentsLab.OrderCreatedConsumer";

    public async Task ProcessOrderCreatedAsync(
        IntegrationEventEnvelope<OrderCreatedPayload> envelope,
        string rawMessage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("OrderCreated received {MessageId} for order {OrderId}", envelope.MessageId, envelope.Payload.OrderId);

        // Kafka entrega mensagens em modelo at-least-once. A Inbox torna o consumidor idempotente:
        // se a mesma mensagem voltar, o efeito de negocio nao sera aplicado duas vezes.
        if (await inboxRepository.ExistsAsync(envelope.MessageId, OrderCreatedConsumerName, cancellationToken))
        {
            logger.LogInformation("Duplicate OrderCreated ignored {MessageId}", envelope.MessageId);
            return;
        }

        var order = await orderRepository.GetByIdAsync(envelope.Payload.OrderId, cancellationToken);
        if (order is null)
        {
            await MoveEnvelopeToDeadLetterAsync(envelope, rawMessage, "Order was not found", 0, cancellationToken);
            return;
        }

        var payment = await GetOrCreatePaymentAsync(order, envelope, cancellationToken);

        if (payment.Status is PaymentStatus.Approved)
        {
            logger.LogInformation("Existing approved payment found {PaymentId} for order {OrderId}", payment.Id, order.Id);
            await SaveInboxOnlyAsync(envelope, cancellationToken);
            return;
        }

        await ProcessPaymentAttemptAsync(payment, order, envelope.MessageId, envelope.CorrelationId, envelope.CausationId, rawMessage, saveInbox: true, cancellationToken);
    }

    public async Task MoveInvalidMessageToDeadLetterAsync(string rawMessage, string errorMessage, CancellationToken cancellationToken)
    {
        var messageId = CreateDeterministicMessageId(rawMessage);
        if (await deadLetterRepository.ExistsAsync(messageId, OrderCreatedConsumerName, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await deadLetterRepository.AddAsync(
            new DeadLetterMessage(
                Guid.NewGuid(),
                messageId,
                kafkaOptions.Value.Topics.OrderCreated,
                OrderCreatedConsumerName,
                "InvalidMessage",
                Guid.Empty,
                JsonSerializer.Serialize(new { rawMessage }),
                errorMessage,
                0,
                now),
            cancellationToken);
        await inboxRepository.AddAsync(new InboxMessage(messageId, OrderCreatedConsumerName, "InvalidMessage", Guid.Empty, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogError("Message moved to DLQ {MessageId}. Reason: {Reason}", messageId, errorMessage);
    }

    public async Task ProcessRetryAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId} was not found");

        if (payment.Status is not PaymentStatus.RetryScheduled)
        {
            return;
        }

        var order = await orderRepository.GetByIdAsync(payment.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {payment.OrderId} was not found");

        await ProcessPaymentAttemptAsync(
            payment,
            order,
            payment.OriginalMessageId,
            payment.OriginalMessageId,
            payment.OriginalMessageId,
            JsonSerializer.Serialize(new { payment.OrderId, payment.IdempotencyKey }),
            saveInbox: false,
            cancellationToken);
    }

    private async Task<Payment> GetOrCreatePaymentAsync(
        Order order,
        IntegrationEventEnvelope<OrderCreatedPayload> envelope,
        CancellationToken cancellationToken)
    {
        // A chave deterministica PAYMENT:{orderId} garante que retries HTTP, redeliveries Kafka e
        // concorrencia no consumidor apontem para uma unica intencao de cobranca.
        var idempotencyKey = idempotencyKeyStrategy.Generate(order.Id);
        logger.LogInformation("Payment idempotency key generated {IdempotencyKey}", idempotencyKey);

        var existing = await paymentRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Existing payment found {PaymentId} for key {IdempotencyKey}", existing.Id, idempotencyKey);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var payment = Payment.Create(
            order.Id,
            order.CustomerId,
            order.Amount,
            order.Currency,
            idempotencyKey,
            envelope.MessageId,
            kafkaOptions.Value.Topics.OrderCreated,
            envelope.EventType,
            now);

        await paymentRepository.AddAsync(payment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return payment;
    }

    private async Task ProcessPaymentAttemptAsync(
        Payment payment,
        Order order,
        Guid originalMessageId,
        Guid correlationId,
        Guid causationId,
        string rawMessage,
        bool saveInbox,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var attemptNumber = payment.AttemptCount + 1;

        if (attemptNumber > paymentsOptions.Value.MaxProcessingAttempts)
        {
            await MovePaymentToDeadLetterAsync(payment, order, rawMessage, "Maximum payment processing attempts exceeded", cancellationToken);
            return;
        }

        payment.MarkAttemptStarted(now);
        var attempt = PaymentAttempt.Start(payment.Id, payment.OrderId, attemptNumber, payment.IdempotencyKey, now);
        await paymentAttemptRepository.AddAsync(attempt, cancellationToken);

        logger.LogInformation("Payment attempt started {PaymentAttemptId} attempt {AttemptNumber} for payment {PaymentId}", attempt.Id, attemptNumber, payment.Id);
        logger.LogInformation("Calling fake payment provider for payment {PaymentId}", payment.Id);

        FakePaymentProviderResponse providerResponse;
        try
        {
            providerResponse = await providerClient.PayAsync(
                new FakePaymentProviderRequest(payment.IdempotencyKey, payment.OrderId, payment.Amount, payment.Currency, null),
                cancellationToken);
        }
        catch (TaskCanceledException exception)
        {
            await HandleRetryableFailureAsync(payment, order, attempt, "Provider call timed out", saveInbox, originalMessageId, cancellationToken);
            logger.LogWarning(exception, "Fake provider timeout for payment {PaymentId}", payment.Id);
            return;
        }

        switch (providerResponse.Status)
        {
            case FakePaymentProviderStatus.Approved:
                await HandleApprovedAsync(payment, order, attempt, providerResponse, saveInbox, originalMessageId, correlationId, causationId, cancellationToken);
                break;
            case FakePaymentProviderStatus.Rejected:
                await HandlePermanentFailureAsync(payment, order, attempt, providerResponse, saveInbox, originalMessageId, correlationId, causationId, cancellationToken);
                break;
            case FakePaymentProviderStatus.TemporaryFailure:
            case FakePaymentProviderStatus.Timeout:
                await HandleRetryableFailureAsync(payment, order, attempt, providerResponse.Message, saveInbox, originalMessageId, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported provider response {providerResponse.Status}");
        }
    }

    private async Task HandleApprovedAsync(
        Payment payment,
        Order order,
        PaymentAttempt attempt,
        FakePaymentProviderResponse providerResponse,
        bool saveInbox,
        Guid inboxMessageId,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        payment.MarkApproved(providerResponse.ProviderTransactionId ?? string.Empty, now);
        order.MarkAsPaid(now);
        attempt.MarkApproved(providerResponse.ProviderTransactionId ?? string.Empty, providerResponse.ResponseCode, providerResponse.Message, now);

        if (saveInbox)
        {
            await inboxRepository.AddAsync(new InboxMessage(inboxMessageId, OrderCreatedConsumerName, "OrderCreated", order.Id, now), cancellationToken);
        }

        await outboxRepository.AddAsync(eventBuilder.BuildPaymentApproved(payment, correlationId, causationId, now), cancellationToken);

        // O offset Kafka so sera commitado depois deste commit do banco. Assim, se a API cair antes,
        // o broker pode entregar novamente e a Inbox/Idempotencia impedem cobranca duplicada.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Fake provider approved payment {PaymentId}", payment.Id);
        logger.LogInformation("Payment approved {PaymentId}", payment.Id);
        logger.LogInformation("PaymentApproved outbox message saved for payment {PaymentId}", payment.Id);
    }

    private async Task HandlePermanentFailureAsync(
        Payment payment,
        Order order,
        PaymentAttempt attempt,
        FakePaymentProviderResponse providerResponse,
        bool saveInbox,
        Guid inboxMessageId,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        payment.MarkFailed(providerResponse.Message, now);
        order.MarkAsPaymentFailed(now);
        attempt.MarkPermanentError(providerResponse.ResponseCode, providerResponse.Message, now);

        if (saveInbox)
        {
            await inboxRepository.AddAsync(new InboxMessage(inboxMessageId, OrderCreatedConsumerName, "OrderCreated", order.Id, now), cancellationToken);
        }

        await outboxRepository.AddAsync(eventBuilder.BuildPaymentFailed(payment, providerResponse.Message, correlationId, causationId, now), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Fake provider rejected payment {PaymentId}", payment.Id);
        logger.LogInformation("Payment failed {PaymentId}", payment.Id);
        logger.LogInformation("PaymentFailed outbox message saved for payment {PaymentId}", payment.Id);
    }

    private async Task HandleRetryableFailureAsync(
        Payment payment,
        Order order,
        PaymentAttempt attempt,
        string errorMessage,
        bool saveInbox,
        Guid inboxMessageId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (payment.AttemptCount >= paymentsOptions.Value.MaxProcessingAttempts)
        {
            attempt.MarkRetryableError("MAX_RETRIES", errorMessage, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await MovePaymentToDeadLetterAsync(payment, order, JsonSerializer.Serialize(new { payment.OrderId, payment.IdempotencyKey }), errorMessage, cancellationToken);
            return;
        }

        // Falha temporaria nao vira PaymentFailed: ela agenda retry no banco para dar visibilidade
        // ao estudo e evitar um loop apertado de redelivery Kafka sem commit de offset.
        payment.ScheduleRetry(errorMessage, now.AddSeconds(paymentsOptions.Value.RetryDelaySeconds), now);
        order.MarkAsPaymentPendingRetry(now);
        attempt.MarkRetryableError("TEMP_001", errorMessage, now);

        if (saveInbox)
        {
            await inboxRepository.AddAsync(new InboxMessage(inboxMessageId, OrderCreatedConsumerName, "OrderCreated", order.Id, now), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Fake provider temporary failure for payment {PaymentId}", payment.Id);
        logger.LogInformation("Payment retry scheduled {PaymentId} next retry at {NextRetryAt}", payment.Id, payment.NextRetryAt);
    }

    private async Task SaveInboxOnlyAsync(IntegrationEventEnvelope<OrderCreatedPayload> envelope, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await inboxRepository.AddAsync(new InboxMessage(envelope.MessageId, OrderCreatedConsumerName, envelope.EventType, envelope.AggregateId, DateTimeOffset.UtcNow), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MoveEnvelopeToDeadLetterAsync(
        IntegrationEventEnvelope<OrderCreatedPayload> envelope,
        string rawMessage,
        string errorMessage,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (await deadLetterRepository.ExistsAsync(envelope.MessageId, OrderCreatedConsumerName, cancellationToken))
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await deadLetterRepository.AddAsync(
            new DeadLetterMessage(
                Guid.NewGuid(),
                envelope.MessageId,
                kafkaOptions.Value.Topics.OrderCreated,
                OrderCreatedConsumerName,
                envelope.EventType,
                envelope.AggregateId,
                rawMessage,
                errorMessage,
                retryCount,
                now),
            cancellationToken);
        await inboxRepository.AddAsync(new InboxMessage(envelope.MessageId, OrderCreatedConsumerName, envelope.EventType, envelope.AggregateId, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogError("Message moved to DLQ {MessageId}. Reason: {Reason}", envelope.MessageId, errorMessage);
    }

    private async Task MovePaymentToDeadLetterAsync(
        Payment payment,
        Order order,
        string payload,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (await deadLetterRepository.ExistsAsync(payment.OriginalMessageId, OrderCreatedConsumerName, cancellationToken))
        {
            payment.MarkDeadLettered(errorMessage, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        payment.MarkDeadLettered(errorMessage, now);
        order.MarkAsPaymentFailed(now);
        await deadLetterRepository.AddAsync(
            new DeadLetterMessage(
                Guid.NewGuid(),
                payment.OriginalMessageId,
                payment.OriginalTopic,
                OrderCreatedConsumerName,
                payment.OriginalEventType,
                payment.OrderId,
                payload,
                errorMessage,
                payment.AttemptCount,
                now),
            cancellationToken);
        await outboxRepository.AddAsync(eventBuilder.BuildPaymentMovedToDeadLetter(payment, errorMessage, payment.OriginalMessageId, payment.OriginalMessageId, now), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // DLQ separa mensagens que nao devem continuar consumindo recursos de retry. Ela preserva
        // payload, erro e contador para analise manual sem travar a fila principal.
        logger.LogError("Message moved to DLQ {MessageId}. Reason: {Reason}", payment.OriginalMessageId, errorMessage);
    }

    private static Guid CreateDeterministicMessageId(string rawMessage)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawMessage));
        return new Guid(hash[..16]);
    }
}
