namespace CloudNativePaymentsLab.Api.Modules.Messaging.Application;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public KafkaTopics Topics { get; init; } = new();
    public string ConsumerGroupId { get; init; } = "cloud-native-payments-lab";
}

public sealed class KafkaTopics
{
    public string OrderCreated { get; init; } = "orders.order-created";
    public string PaymentApproved { get; init; } = "payments.payment-approved";
    public string PaymentFailed { get; init; } = "payments.payment-failed";
    public string PaymentsDeadLetter { get; init; } = "payments.dlq";
}
