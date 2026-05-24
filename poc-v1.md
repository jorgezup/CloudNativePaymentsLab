Você é um arquiteto de software sênior e engenheiro backend especialista em .NET, PostgreSQL, Kafka, arquitetura orientada a eventos, Outbox Pattern, Inbox Pattern, idempotência e boas práticas de design de software.

Quero iniciar uma POC de estudo chamada CloudNativePaymentsLab.

Objetivo da etapa 1:
Criar um monólito modular em .NET 10, usando PostgreSQL e Kafka, com Outbox Pattern e Inbox Pattern implementados manualmente com polling.

Ao final desta etapa, eu quero conseguir:
1. Subir as dependências com Docker Compose.
2. Executar uma API .NET 10 localmente.
3. Criar um pedido via endpoint HTTP.
4. Salvar o pedido no PostgreSQL.
5. Salvar um evento na tabela de Outbox dentro da mesma transação do pedido.
6. Ter um Outbox Publisher em background lendo mensagens pendentes da tabela OutboxMessages e publicando no Kafka.
7. Ter um Kafka Consumer em background consumindo o evento publicado.
8. Registrar o processamento do evento na tabela InboxMessages.
9. Conseguir verificar no banco que:
   - o pedido foi salvo;
   - a mensagem foi salva na Outbox;
   - a mensagem foi publicada;
   - o evento foi consumido;
   - o processamento foi registrado na Inbox.
10. Ter logs claros no console para estudar o fluxo completo.

Stack obrigatória:
- .NET 10
- ASP.NET Core Web API
- PostgreSQL
- Kafka
- Docker Compose para PostgreSQL, Kafka e Kafka UI
- Entity Framework Core
- Npgsql
- Confluent.Kafka
- Serilog ou logging nativo bem estruturado
- BackgroundService para polling da Outbox
- BackgroundService para consumo do Kafka

Arquitetura:
Usar monólito modular, mas com separação clara de camadas e responsabilidades.

Estrutura sugerida da solution:

CloudNativePaymentsLab/
├── src/
│   ├── CloudNativePaymentsLab.Api/
│   │   ├── Modules/
│   │   │   ├── Orders/
│   │   │   │   ├── Application/
│   │   │   │   ├── Domain/
│   │   │   │   ├── Infrastructure/
│   │   │   │   └── Presentation/
│   │   │   └── Messaging/
│   │   │       ├── Application/
│   │   │       ├── Domain/
│   │   │       └── Infrastructure/
│   │   ├── BuildingBlocks/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   └── Infrastructure/
│   │   └── Program.cs
│   │
├── docker/
│   └── docker-compose.yml
│
├── scripts/
│   └── init.sql
│
└── README.md

Pode ajustar a estrutura se fizer sentido, mas mantenha a separação entre:
- domínio;
- aplicação;
- infraestrutura;
- apresentação;
- messaging;
- background workers.

Domínio inicial:
Criar um contexto simples de pedidos e pagamentos.

Entidade Order:
- Id
- CustomerId
- Amount
- Currency
- Status
- IdempotencyKey
- CreatedAt
- UpdatedAt

Status possíveis:
- Created
- Processing
- Paid
- Failed

Endpoint inicial:
POST /orders

Request:
{
  "customerId": "string",
  "amount": 100.50,
  "currency": "BRL"
}

Response:
{
  "orderId": "guid",
  "status": "Created",
  "idempotencyKey": "string"
}

Regras importantes:
1. Ao criar o pedido, gerar uma chave de idempotência determinística.
2. A chave NÃO deve ser aleatória.
3. Para esta POC, a idempotency key do pedido deve ser baseada em dados estáveis da operação.
4. Como ainda não temos um externalRequestId enviado pelo cliente, crie um campo opcional no request chamado externalReference.
5. Se externalReference vier preenchido, a idempotency key deve ser:
   ORDER:{customerId}:{externalReference}
6. Se externalReference não vier preenchido, gerar uma externalReference internamente, mas documentar no comentário do código que, em sistemas reais, a referência deveria vir do cliente ou de um processo anterior para permitir reprocessamento determinístico.
7. Criar índice único em Orders.IdempotencyKey.
8. Se uma requisição chegar com a mesma idempotency key, não criar outro pedido. Retornar o pedido existente.

Outbox Pattern:
Criar tabela OutboxMessages com os campos:
- Id
- AggregateId
- AggregateType
- EventType
- Payload
- Status
- RetryCount
- CreatedAt
- PublishedAt
- LastError
- CorrelationId
- CausationId

Status da Outbox:
- Pending
- Processing
- Published
- Failed

Regras da Outbox:
1. Ao criar um pedido, salvar o pedido e a OutboxMessage dentro da mesma transação do PostgreSQL.
2. A mensagem de Outbox deve representar o evento OrderCreated.
3. O Outbox Publisher deve ser um BackgroundService.
4. O Outbox Publisher deve fazer polling da tabela OutboxMessages.
5. O Outbox Publisher deve buscar mensagens com status Pending ou Failed com RetryCount menor que 5.
6. Antes de publicar, marcar a mensagem como Processing.
7. Publicar no Kafka.
8. Após publicar com sucesso, marcar como Published e preencher PublishedAt.
9. Em caso de erro, marcar como Failed, incrementar RetryCount e preencher LastError.
10. Implementar controle simples para evitar que duas instâncias publiquem a mesma mensagem ao mesmo tempo.
11. Usar transação e lock otimista ou pessimista no PostgreSQL.
12. Criar índices úteis para busca por Status, CreatedAt e RetryCount.

Kafka:
Criar tópico:
- orders.order-created

A mensagem publicada no Kafka deve ter um envelope, por exemplo:
{
  "messageId": "guid",
  "eventType": "OrderCreated",
  "aggregateId": "guid",
  "aggregateType": "Order",
  "correlationId": "guid",
  "causationId": "guid",
  "occurredAt": "datetime",
  "payload": {
    "orderId": "guid",
    "customerId": "string",
    "amount": 100.50,
    "currency": "BRL",
    "idempotencyKey": "string"
  }
}

A key da mensagem no Kafka deve ser o AggregateId do pedido.

Inbox Pattern:
Criar tabela InboxMessages com os campos:
- MessageId
- ConsumerName
- EventType
- AggregateId
- ProcessedAt
- CreatedAt

Chave primária ou índice único:
- MessageId + ConsumerName

Criar um Kafka Consumer dentro da própria aplicação usando BackgroundService.

Regras do consumidor:
1. Consumir mensagens do tópico orders.order-created.
2. Antes de processar, verificar se já existe registro na InboxMessages para MessageId + ConsumerName.
3. Se já existir, ignorar a mensagem e logar que era duplicada.
4. Se não existir, processar a mensagem.
5. Para a etapa 1, o processamento pode apenas:
   - registrar log;
   - atualizar o status do pedido de Created para Processing;
   - salvar registro na InboxMessages.
6. Salvar a atualização do pedido e o registro de Inbox dentro da mesma transação.
7. O commit do offset no Kafka deve acontecer somente depois do processamento no banco ter sucesso.
8. Desabilitar auto commit no Kafka Consumer.
9. Em caso de erro, não registrar Inbox e não commitar offset.

Design Patterns obrigatórios:
Implementar, de forma simples e sem overengineering:

1. Repository Pattern:
   - IOrderRepository
   - OrderRepository
   - IOutboxRepository
   - OutboxRepository
   - IInboxRepository
   - InboxRepository

2. Strategy Pattern:
   Criar uma estratégia para geração de chave de idempotência.
   - IIdempotencyKeyStrategy
   - OrderIdempotencyKeyStrategy

3. Builder Pattern:
   Criar um builder para montar mensagens de integração.
   - IntegrationEventBuilder
   ou
   - OutboxMessageBuilder

4. Unit of Work:
   Usar o DbContext como Unit of Work.
   Não criar uma abstração desnecessária se não for útil, mas documentar nos comentários que o EF Core DbContext está atuando como Unit of Work.

5. Background Worker Pattern:
   - OutboxPublisherWorker
   - OrderCreatedConsumerWorker

Comentários no código:
Como essa é uma POC de estudo, adicionar comentários nos principais métodos explicando:
- por que a OutboxMessage é salva na mesma transação do pedido;
- por que publicar direto no Kafka dentro do request pode ser arriscado;
- por que o consumidor precisa ser idempotente;
- por que a idempotency key deve ser determinística;
- por que o offset do Kafka só deve ser commitado depois do sucesso no banco;
- por que a Inbox evita processamento duplicado;
- por que índices são importantes para polling da Outbox.

Banco de dados:
Usar migrations do Entity Framework Core ou scripts SQL versionados.

Criar índices:
Orders:
- índice único em IdempotencyKey
- índice em CustomerId
- índice em Status
- índice em CreatedAt

OutboxMessages:
- índice em Status
- índice em CreatedAt
- índice composto em Status, RetryCount, CreatedAt
- índice em AggregateId
- índice em CorrelationId

InboxMessages:
- índice único em MessageId, ConsumerName
- índice em AggregateId
- índice em EventType
- índice em ProcessedAt

Docker Compose:
Criar docker-compose.yml com:
- PostgreSQL
- Kafka
- Kafka UI

Configurações esperadas:
PostgreSQL:
- database: cloud_native_payments
- user: postgres
- password: postgres
- port: 5432

Kafka:
- porta externa 9092
- tópico orders.order-created
- broker simples para ambiente local
- pode usar Redpanda se for mais simples que Kafka/Zookeeper, mas explique a escolha no README.
- Se usar Kafka tradicional, configurar corretamente listeners internos e externos.

Kafka UI:
- expor em http://localhost:8080

API:
- expor em http://localhost:5000 ou http://localhost:8081
- Swagger habilitado em desenvolvimento.

Endpoints mínimos:
1. POST /orders
2. GET /orders/{id}
3. GET /outbox
4. GET /inbox
5. GET /health

Os endpoints /outbox e /inbox são apenas para estudo/debug nesta etapa.

Configuração:
Usar appsettings.json com:
- ConnectionStrings:Postgres
- Kafka:BootstrapServers
- Kafka:Topics:OrderCreated
- Kafka:ConsumerGroupId
- Outbox:PollingIntervalSeconds
- Outbox:BatchSize
- Outbox:MaxRetryCount

Critérios de aceite:
Ao rodar:

docker compose up -d

e depois executar a API, eu devo conseguir chamar:

POST /orders

Exemplo:
{
  "customerId": "customer-001",
  "amount": 199.90,
  "currency": "BRL",
  "externalReference": "checkout-abc-001"
}

Resultado esperado:
1. Um pedido é criado na tabela Orders.
2. Uma mensagem OrderCreated é criada na tabela OutboxMessages com status Pending.
3. O OutboxPublisherWorker publica a mensagem no Kafka.
4. A mensagem na OutboxMessages muda para Published.
5. O OrderCreatedConsumerWorker consome a mensagem do Kafka.
6. O pedido muda de Created para Processing.
7. Um registro é criado na InboxMessages.
8. Se o mesmo evento for recebido novamente, ele não deve ser processado duas vezes.
9. Se eu chamar POST /orders novamente com mesmo customerId e externalReference, não deve criar novo pedido; deve retornar o pedido já existente.

README:
Criar um README.md explicando:
- objetivo da POC;
- arquitetura;
- como rodar;
- como subir Docker Compose;
- como aplicar migrations;
- como chamar os endpoints;
- como verificar o banco;
- como acessar Kafka UI;
- explicação resumida de Outbox Pattern;
- explicação resumida de Inbox Pattern;
- explicação sobre idempotência determinística;
- próximos passos sugeridos.

Evite nesta etapa:
- Kubernetes
- Prometheus
- Grafana
- Kibana
- OpenTelemetry
- autenticação/autorização
- múltiplos microserviços
- SignalR/WebSocket
- blue/green deployment
- testes de carga

Esses itens ficarão para etapas futuras.

Entregue:
1. Solution .NET completa.
2. Código compilável.
3. Docker Compose funcional.
4. Migrations ou scripts SQL.
5. README.
6. Comentários nos principais métodos.
7. Logs claros do fluxo:
   - Order created
   - Outbox message saved
   - Outbox message publishing
   - Outbox message published
   - Kafka message consumed
   - Inbox message saved
   - Duplicate message ignored