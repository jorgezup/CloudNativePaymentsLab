using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;

public sealed class PaymentRetryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PaymentsOptions> options,
    ILogger<PaymentRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRetriesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment retry worker loop failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryPollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessDueRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<PaymentProcessingService>();

        var duePayments = await paymentRepository.GetDueRetriesAsync(DateTimeOffset.UtcNow, batchSize: 10, cancellationToken);
        foreach (var payment in duePayments)
        {
            // Este retry e de processamento/chamada externa, diferente do retry da Outbox.
            // A Outbox reenvia eventos ao Kafka; aqui reexecutamos a tentativa de cobranca agendada no banco.
            logger.LogInformation("Processing scheduled payment retry {PaymentId}", payment.Id);
            await processor.ProcessRetryAsync(payment.Id, cancellationToken);
        }
    }
}
