using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(producer, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox publisher loop failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(outboxOptions.Value.PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(IProducer<string, string> producer, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var messages = await outboxRepository.LockNextBatchAsync(outboxOptions.Value.BatchSize, outboxOptions.Value.MaxRetryCount, cancellationToken);

        foreach (var message in messages)
        {
            // Publicar direto no request HTTP seria arriscado: uma falha apos salvar o pedido poderia perder o evento.
            // A Outbox permite retry assíncrono mantendo a decisao de negocio persistida no banco.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                message.MarkAsProcessing();
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Outbox message publishing {MessageId} to topic {Topic}", message.Id, message.Topic);
                await producer.ProduceAsync(
                    message.Topic,
                    new Message<string, string>
                    {
                        Key = message.AggregateId.ToString(),
                        Value = message.Payload
                    },
                    cancellationToken);

                message.MarkAsPublished(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Outbox message published {MessageId}", message.Id);
            }
            catch (Exception exception)
            {
                message.MarkAsFailed(exception.Message);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogError(exception, "Outbox message failed {MessageId}", message.Id);
            }
        }
    }
}
