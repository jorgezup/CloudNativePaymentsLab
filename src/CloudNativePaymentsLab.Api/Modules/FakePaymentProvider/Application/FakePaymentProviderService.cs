using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

public sealed class FakePaymentProviderService(
    IEnumerable<IPaymentProviderStrategy> strategies,
    IOptions<FakePaymentProviderOptions> options,
    ILogger<FakePaymentProviderService> logger)
{
    private readonly ConcurrentDictionary<string, FakePaymentProviderResponse> terminalResults = new();

    public async Task<FakePaymentProviderResponse> PayAsync(FakePaymentProviderRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        if (terminalResults.TryGetValue(request.IdempotencyKey, out var cached))
        {
            logger.LogInformation("Fake provider idempotency hit for key {IdempotencyKey}", request.IdempotencyKey);
            return cached;
        }

        if (options.Value.ArtificialDelayMs > 0)
        {
            await Task.Delay(options.Value.ArtificialDelayMs, cancellationToken);
        }

        var status = ResolveForcedStatus(request.ForceResult) ?? ResolveStrategy().Choose(request);
        var response = CreateResponse(request, status);

        // O provedor fake simula idempotencia do fornecedor externo: uma cobranca aprovada ou recusada
        // precisa retornar o mesmo resultado para a mesma chave, sem gerar uma segunda transacao.
        if (response.Status is FakePaymentProviderStatus.Approved or FakePaymentProviderStatus.Rejected)
        {
            terminalResults.TryAdd(request.IdempotencyKey, response);
        }

        return response;
    }

    private IPaymentProviderStrategy ResolveStrategy()
    {
        var strategyName = options.Value.Mode.Trim();
        return strategies.FirstOrDefault(strategy => strategy.GetType().Name.StartsWith(strategyName, StringComparison.OrdinalIgnoreCase))
            ?? strategies.OfType<RandomPaymentProviderStrategy>().First();
    }

    private static FakePaymentProviderStatus? ResolveForcedStatus(string? forceResult) =>
        string.IsNullOrWhiteSpace(forceResult)
            ? null
            : Enum.TryParse<FakePaymentProviderStatus>(forceResult, ignoreCase: true, out var status)
                ? status
                : throw new ArgumentException("forceResult must be Approved, TemporaryFailure, Rejected or Timeout");

    private static FakePaymentProviderResponse CreateResponse(FakePaymentProviderRequest request, FakePaymentProviderStatus status) =>
        status switch
        {
            FakePaymentProviderStatus.Approved => new(
                $"fake-{request.OrderId:N}",
                FakePaymentProviderStatus.Approved,
                "00",
                "Payment approved"),
            FakePaymentProviderStatus.TemporaryFailure => new(
                null,
                FakePaymentProviderStatus.TemporaryFailure,
                "TEMP_001",
                "Temporary provider instability"),
            FakePaymentProviderStatus.Rejected => new(
                null,
                FakePaymentProviderStatus.Rejected,
                "CARD_DECLINED",
                "Payment rejected"),
            FakePaymentProviderStatus.Timeout => new(
                null,
                FakePaymentProviderStatus.Timeout,
                "TIMEOUT",
                "Provider timeout"),
            _ => throw new InvalidOperationException($"Unsupported provider status {status}")
        };

    private static void Validate(FakePaymentProviderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("idempotencyKey is required");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("amount must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            throw new ArgumentException("currency must use a 3-letter ISO code");
        }
    }
}
