# CloudNativePaymentsLab

POC de estudo em .NET 10 para praticar monolito modular, PostgreSQL, Kafka-compatible broker, Outbox Pattern, Inbox Pattern, idempotencia e workers em background.

Nesta etapa, a propria API publica e consome o evento Kafka de `OrderCreated`. Isso e proposital para estudar confiabilidade de publicacao, consumo idempotente e rastreabilidade antes de dividir em microservicos.

## Arquitetura

- `CloudNativePaymentsLab.Api`: ASP.NET Core Web API em .NET 10.
- `Modules/Orders`: dominio, aplicacao, infraestrutura e endpoints de pedidos.
- `Modules/Messaging`: Outbox, Inbox, builders de eventos e workers Kafka.
- `BuildingBlocks/Infrastructure`: `PaymentsDbContext`, usado como Unit of Work do EF Core.
- PostgreSQL guarda `Orders`, `OutboxMessages` e `InboxMessages`.
- Redpanda fornece uma API Kafka local sem Zookeeper, simplificando a POC.

## Como rodar

Suba as dependencias:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Aplique as migrations:

```bash
DOTNET_CLI_HOME=/tmp dotnet ef database update --project src/CloudNativePaymentsLab.Api
```

Execute a API:

```bash
DOTNET_CLI_HOME=/tmp dotnet run --project src/CloudNativePaymentsLab.Api
```

Endpoints:

- API: `http://localhost:8081`
- Health: `GET http://localhost:8081/health`
- Kafka UI: `http://localhost:8080`
- Kafka topic: `orders.order-created`

## Criar pedido

```bash
curl -X POST http://localhost:8081/orders \
  -H "content-type: application/json" \
  -d '{
    "customerId": "customer-001",
    "amount": 199.90,
    "currency": "BRL",
    "externalReference": "checkout-abc-001"
  }'
```

Com o mesmo `customerId` e `externalReference`, a API retorna o pedido existente porque a idempotency key e deterministica: `ORDER:{customerId}:{externalReference}`.

## Endpoints de estudo

```bash
curl http://localhost:8081/orders/{orderId}
curl http://localhost:8081/outbox
curl http://localhost:8081/inbox
curl http://localhost:8081/health
```

## Verificar no banco

```bash
docker exec -it cloudnativepayments-postgres psql -U postgres -d cloud_native_payments
```

Consultas uteis:

```sql
SELECT * FROM "Orders" ORDER BY "CreatedAt" DESC;
SELECT * FROM "OutboxMessages" ORDER BY "CreatedAt" DESC;
SELECT * FROM "InboxMessages" ORDER BY "CreatedAt" DESC;
```

## Fluxo esperado

1. `POST /orders` cria o pedido.
2. Pedido e `OutboxMessage` sao salvos na mesma transacao.
3. `OutboxPublisherWorker` busca mensagens `Pending` ou `Failed`.
4. Worker marca a mensagem como `Processing`, publica no Kafka e marca como `Published`.
5. `OrderCreatedConsumerWorker` consome a mensagem.
6. Consumer verifica `InboxMessages` para evitar duplicidade.
7. Consumer atualiza o pedido de `Created` para `Processing` e salva Inbox na mesma transacao.
8. Offset Kafka e commitado somente depois do sucesso no banco.

## Conceitos

Outbox Pattern evita perder eventos quando a operacao de negocio foi salva, mas a publicacao no broker falhou. A aplicacao grava o evento em uma tabela na mesma transacao e um worker publica depois com retry.

Inbox Pattern torna o consumidor idempotente. Como brokers podem entregar a mesma mensagem mais de uma vez, o consumidor registra `MessageId + ConsumerName` antes de commitar o offset.

Idempotencia deterministica evita pedidos duplicados em retries HTTP. Nesta POC, quando o cliente envia `externalReference`, a chave e baseada em dados estaveis da operacao.

## Proximos passos

- Adicionar testes automatizados.
- Separar producer/consumer em processos diferentes.
- Adicionar observabilidade com OpenTelemetry.
- Evoluir status de pagamento para `Paid` e `Failed`.
- Adicionar autenticação e autorizacao.
