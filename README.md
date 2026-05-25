# CloudNativePaymentsLab

POC de estudo em .NET 10 para praticar monolito modular, PostgreSQL, Kafka-compatible broker, Outbox Pattern, Inbox Pattern, idempotencia, pagamentos assincronos, retry, DLQ e workers em background.

Nesta etapa, a propria API publica e consome eventos Kafka. Isso e proposital para estudar confiabilidade de publicacao, consumo idempotente, pagamentos externos fake e rastreabilidade antes de dividir em microservicos.

## Arquitetura

- `CloudNativePaymentsLab.Api`: ASP.NET Core Web API em .NET 10.
- `Modules/Orders`: dominio, aplicacao, infraestrutura e endpoints de pedidos.
- `Modules/Messaging`: Outbox, Inbox, builders de eventos e workers Kafka.
- `Modules/Payments`: dominio, processamento de pagamentos, tentativas, retry, DLQ e endpoints de consulta.
- `Modules/FakePaymentProvider`: API fake de provedor externo com idempotencia por chave.
- `BuildingBlocks/Infrastructure`: `PaymentsDbContext`, usado como Unit of Work do EF Core.
- PostgreSQL guarda `Orders`, `Payments`, `PaymentAttempts`, `DeadLetterMessages`, `OutboxMessages` e `InboxMessages`.
- Redpanda fornece uma API Kafka local sem Zookeeper, simplificando a POC.

Fluxo da etapa 2:

```text
POST /orders
  -> Orders + OutboxMessages(OrderCreated)
  -> OutboxPublisherWorker
  -> Kafka orders.order-created
  -> OrderCreatedConsumerWorker
  -> Payment + PaymentAttempt
  -> POST /fake-provider/payments
  -> Order Paid, PaymentFailed ou PaymentPendingRetry
  -> OutboxMessages(PaymentApproved, PaymentFailed ou PaymentMovedToDeadLetter)
  -> Kafka payments.payment-approved, payments.payment-failed ou payments.dlq
```

## Como rodar

Suba as dependencias:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Aplique as migrations:

```bash
DOTNET_CLI_HOME=/tmp dotnet ef database update --project src/CloudNativePaymentsLab.Api
```

Em `Development`, a API tambem aplica migrations pendentes automaticamente ao iniciar, antes dos workers em background consultarem o banco. O comando manual continua util quando voce quiser atualizar o schema antes de subir a API.

Execute a API:

```bash
DOTNET_CLI_HOME=/tmp dotnet run --project src/CloudNativePaymentsLab.Api
```

Endpoints:

- API: `http://localhost:8081`
- Health: `GET http://localhost:8081/health`
- Kafka UI: `http://localhost:8080`
- Kafka topics: `orders.order-created`, `payments.payment-approved`, `payments.payment-failed`, `payments.dlq`

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

Com o mesmo `customerId` e `externalReference`, a API retorna o pedido existente porque a idempotency key do pedido e deterministica: `ORDER:{customerId}:{externalReference}`.

O pagamento tambem usa chave deterministica: `PAYMENT:{orderId}`. Essa chave e enviada ao Fake Payment Provider para que redelivery de Kafka ou retry nao crie cobranca duplicada.

## Endpoints de estudo

```bash
curl http://localhost:8081/orders/{orderId}
curl http://localhost:8081/payments
curl http://localhost:8081/payments/{paymentId}
curl http://localhost:8081/orders/{orderId}/payment
curl http://localhost:8081/payment-attempts
curl http://localhost:8081/orders/{orderId}/payment-attempts
curl http://localhost:8081/dead-letter
curl http://localhost:8081/dead-letter/{deadLetterId}
curl http://localhost:8081/outbox
curl http://localhost:8081/inbox
curl http://localhost:8081/health
```

## Fake Payment Provider

Endpoint:

```bash
curl -X POST http://localhost:8081/fake-provider/payments \
  -H "content-type: application/json" \
  -d '{
    "idempotencyKey": "PAYMENT:00000000-0000-0000-0000-000000000001",
    "orderId": "00000000-0000-0000-0000-000000000001",
    "amount": 199.90,
    "currency": "BRL",
    "forceResult": "Approved"
  }'
```

`forceResult` aceita `Approved`, `TemporaryFailure`, `Rejected` e `Timeout`. Resultados terminais (`Approved` e `Rejected`) sao cacheados por `idempotencyKey`; falhas temporarias nao sao cacheadas para permitir retry posterior.

## Verificar no banco

```bash
docker exec -it cloudnativepayments-postgres psql -U postgres -d cloud_native_payments
```

Consultas uteis:

```sql
SELECT * FROM "Orders" ORDER BY "CreatedAt" DESC;
SELECT * FROM "Payments" ORDER BY "CreatedAt" DESC;
SELECT * FROM "PaymentAttempts" ORDER BY "CreatedAt" DESC;
SELECT * FROM "DeadLetterMessages" ORDER BY "CreatedAt" DESC;
SELECT * FROM "OutboxMessages" ORDER BY "CreatedAt" DESC;
SELECT * FROM "InboxMessages" ORDER BY "CreatedAt" DESC;
```

## Fluxo esperado

1. `POST /orders` cria o pedido.
2. Pedido e `OutboxMessage(OrderCreated)` sao salvos na mesma transacao.
3. `OutboxPublisherWorker` busca mensagens `Pending` ou `Failed`.
4. Worker marca a mensagem como `Processing`, publica no Kafka e marca como `Published`.
5. `OrderCreatedConsumerWorker` consome a mensagem.
6. Consumer verifica `InboxMessages` para evitar duplicidade.
7. Consumer cria ou reutiliza `Payment` pela chave `PAYMENT:{orderId}`.
8. Consumer cria `PaymentAttempt` e chama o Fake Payment Provider.
9. Em sucesso, `Payment` vira `Approved`, `Order` vira `Paid`, Inbox e `OutboxMessage(PaymentApproved)` sao salvos na mesma transacao.
10. Em rejeicao permanente, `Payment` vira `Failed`, `Order` vira `PaymentFailed`, Inbox e `OutboxMessage(PaymentFailed)` sao salvos na mesma transacao.
11. Em falha temporaria, `Payment` vira `RetryScheduled`, `Order` vira `PaymentPendingRetry` e o retry fica agendado no banco.
12. Offset Kafka e commitado somente depois do sucesso no banco.

## Testar cenarios

Sucesso:

1. Configure `FakePaymentProvider:Mode` como `AlwaysApprove` ou chame diretamente o provider com `forceResult: Approved`.
2. Crie um pedido em `POST /orders`.
3. Consulte `/orders/{orderId}/payment`, `/orders/{orderId}/payment-attempts`, `/outbox` e `/inbox`.

Falha temporaria:

1. Configure `FakePaymentProvider:Mode` como `AlwaysTemporaryFail`.
2. Crie um pedido.
3. Consulte `/orders/{orderId}/payment-attempts`; novas tentativas aparecem conforme `Payments:RetryDelaySeconds`.
4. Apos `Payments:MaxProcessingAttempts`, a mensagem vai para `/dead-letter`.

Falha permanente:

1. Configure `FakePaymentProvider:Mode` como `AlwaysReject`.
2. Crie um pedido.
3. O pedido deve virar `PaymentFailed`, o pagamento deve virar `Failed` e a Outbox deve conter `PaymentFailed`.

Duplicidade:

1. Reenvie o mesmo `POST /orders` com o mesmo `customerId` e `externalReference`.
2. A API retorna o mesmo pedido.
3. Se Kafka redeliver o mesmo `OrderCreated`, `InboxMessages` evita reprocessamento e a chave `PAYMENT:{orderId}` evita nova cobranca.

## Conceitos

Outbox Pattern evita perder eventos quando a operacao de negocio foi salva, mas a publicacao no broker falhou. A aplicacao grava o evento em uma tabela na mesma transacao e um worker publica depois com retry.

Inbox Pattern torna o consumidor idempotente. Como brokers podem entregar a mesma mensagem mais de uma vez, o consumidor registra `MessageId + ConsumerName` antes de commitar o offset.

Idempotencia deterministica evita pedidos e cobrancas duplicadas. Nesta POC, quando o cliente envia `externalReference`, a chave do pedido e baseada em dados estaveis da operacao. Para pagamento, a chave e `PAYMENT:{orderId}`.

Retry de Outbox e diferente de retry de pagamento. A Outbox tenta publicar eventos no Kafka. O retry de pagamento tenta novamente uma chamada externa que falhou temporariamente. Nesta etapa, o retry de pagamento e controlado no banco com `PaymentAttempts`, `Payment.AttemptCount` e `Payment.NextRetryAt`.

DLQ guarda mensagens que nao devem continuar em retry, como payload invalido, pedido inexistente ou excesso de tentativas. Isso evita loop infinito e preserva payload, erro e contador para analise.

`PaymentApproved` e `PaymentFailed` tambem usam Outbox porque sao eventos de negocio: a mudanca no banco e a publicacao para Kafka precisam continuar consistentes.

## Proximos passos

- Separar producer/consumer em processos diferentes.
- Adicionar observabilidade com OpenTelemetry.
- Adicionar autenticação e autorizacao.
