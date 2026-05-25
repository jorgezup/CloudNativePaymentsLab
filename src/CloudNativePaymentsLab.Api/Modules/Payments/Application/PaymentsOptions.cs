namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public sealed class PaymentsOptions
{
    public int MaxProcessingAttempts { get; init; } = 5;
    public int RetryDelaySeconds { get; init; } = 5;
    public int RetryPollingIntervalSeconds { get; init; } = 2;
}
