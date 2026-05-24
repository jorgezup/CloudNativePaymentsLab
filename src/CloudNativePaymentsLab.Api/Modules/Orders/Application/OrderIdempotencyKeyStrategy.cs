namespace CloudNativePaymentsLab.Api.Modules.Orders.Application;

public sealed class OrderIdempotencyKeyStrategy : IIdempotencyKeyStrategy
{
    public string Generate(string customerId, string? externalReference)
    {
        // A chave precisa ser deterministica para permitir reprocessamento e retry sem duplicar pedidos.
        // Nesta POC geramos uma referencia interna quando ela nao vem do cliente, mas em sistemas reais
        // essa referencia deveria vir do cliente ou de um processo anterior para ser repetivel.
        var stableReference = string.IsNullOrWhiteSpace(externalReference)
            ? $"internal-{Guid.NewGuid():N}"
            : externalReference.Trim();

        return $"ORDER:{customerId.Trim()}:{stableReference}";
    }
}
