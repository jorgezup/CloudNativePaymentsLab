namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public sealed class OutboxOptions
{
    public int PollingIntervalSeconds { get; init; } = 2;
    public int BatchSize { get; init; } = 10;
    public int MaxRetryCount { get; init; } = 5;
}
