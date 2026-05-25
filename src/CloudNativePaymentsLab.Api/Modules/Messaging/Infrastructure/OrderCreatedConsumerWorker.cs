using System.Text.Json;
using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;

public sealed class OrderCreatedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<OrderCreatedConsumerWorker> logger) : BackgroundService
{
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
                logger.LogInformation("Kafka offset committed after database transaction for offset {Offset}", result.Offset);
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
        using var scope = scopeFactory.CreateScope();
        var paymentProcessor = scope.ServiceProvider.GetRequiredService<PaymentProcessingService>();

        try
        {
            var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<OrderCreatedPayload>>(rawMessage, SerializerOptions)
                ?? throw new InvalidOperationException("Invalid OrderCreated message");

            await paymentProcessor.ProcessOrderCreatedAsync(envelope, rawMessage, cancellationToken);
        }
        catch (JsonException exception)
        {
            await paymentProcessor.MoveInvalidMessageToDeadLetterAsync(rawMessage, exception.Message, cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("Invalid OrderCreated message", StringComparison.Ordinal))
        {
            await paymentProcessor.MoveInvalidMessageToDeadLetterAsync(rawMessage, exception.Message, cancellationToken);
        }
    }
}
