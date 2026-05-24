using System.Text.Json.Serialization;
using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using CloudNativePaymentsLab.Api.Modules.Orders.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Orders.Presentation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection("Outbox"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IIdempotencyKeyStrategy, OrderIdempotencyKeyStrategy>();
builder.Services.AddScoped<IntegrationEventBuilder>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<OrderCreatedConsumerWorker>();

builder.Services.AddHealthChecks();

builder.WebHost.UseUrls("http://localhost:8081");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapOrdersEndpoints();
app.MapMessagingDebugEndpoints();

app.Run();
