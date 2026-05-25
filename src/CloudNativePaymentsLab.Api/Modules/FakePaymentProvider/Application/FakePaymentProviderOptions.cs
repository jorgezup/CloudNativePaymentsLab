namespace CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;

public sealed class FakePaymentProviderOptions
{
    public string BaseUrl { get; init; } = "http://localhost:8081";
    public string Mode { get; init; } = "Random";
    public int SuccessRate { get; init; } = 80;
    public int TemporaryFailureRate { get; init; } = 10;
    public int PermanentFailureRate { get; init; } = 5;
    public int TimeoutRate { get; init; } = 5;
    public int ArtificialDelayMs { get; init; } = 200;
}
