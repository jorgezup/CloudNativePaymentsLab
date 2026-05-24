using System.Text.Json;
using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Domain;
using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public sealed class OrderCreatedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<OrderCreatedConsumerWorker> logger) : BackgroundService
{
    private const string ConsumerName = "CloudNativePaymentsLab.OrderCreatedConsumer";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = kafkaOptions.Value.ConsumerGroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(kafkaOptions.Value.Topics.OrderCreated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                ProcessMessageAsync(result.Message.Value, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Kafka message processing failed. Offset was not committed");
            }
        }

        consumer.Close();
    }

    private async Task ProcessMessageAsync(string rawMessage, CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<OrderCreatedPayload>>(rawMessage, SerializerOptions)
            ?? throw new InvalidOperationException("Invalid OrderCreated message");

        logger.LogInformation("Kafka message consumed {MessageId} for order {OrderId}", envelope.MessageId, envelope.Payload.OrderId);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        // Consumidores precisam ser idempotentes porque Kafka pode entregar novamente uma mensagem ja processada.
        if (await inboxRepository.ExistsAsync(envelope.MessageId, ConsumerName, cancellationToken))
        {
            logger.LogInformation("Duplicate message ignored {MessageId} for consumer {ConsumerName}", envelope.MessageId, ConsumerName);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var order = await orderRepository.GetByIdAsync(envelope.Payload.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {envelope.Payload.OrderId} was not found");

        order.MarkAsProcessing(DateTimeOffset.UtcNow);
        await inboxRepository.AddAsync(new InboxMessage(envelope.MessageId, ConsumerName, envelope.EventType, envelope.AggregateId, DateTimeOffset.UtcNow), cancellationToken);

        // A Inbox e a atualizacao do pedido ficam na mesma transacao: sem isso, uma falha no meio poderia
        // registrar o consumo sem aplicar o efeito, ou aplicar o efeito e permitir uma duplicidade depois.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // O offset so deve ser commitado depois do sucesso no banco; caso contrario, o evento poderia ser perdido.
        logger.LogInformation("Inbox message saved {MessageId} for consumer {ConsumerName}", envelope.MessageId, ConsumerName);
    }
}
