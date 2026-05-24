namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public interface IIdempotencyKeyStrategy
{
    string Generate(string customerId, string? externalReference);
}
