using System.Text.Json.Serialization;
using CloudNativePaymentsLab.Api.BuildingBlocks.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Application;
using CloudNativePaymentsLab.Api.Modules.FakePaymentProvider.Presentation;
using CloudNativePaymentsLab.Api.Modules.Messaging.Application;
using CloudNativePaymentsLab.Api.Modules.Messaging.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Orders.Application;
using CloudNativePaymentsLab.Api.Modules.Orders.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Orders.Presentation;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using CloudNativePaymentsLab.Api.Modules.Payments.Presentation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection("Payments"));
builder.Services.Configure<FakePaymentProviderOptions>(builder.Configuration.GetSection("FakePaymentProvider"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IIdempotencyKeyStrategy, OrderIdempotencyKeyStrategy>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentAttemptRepository, PaymentAttemptRepository>();
builder.Services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();
builder.Services.AddScoped<IPaymentIdempotencyKeyStrategy, PaymentIdempotencyKeyStrategy>();
builder.Services.AddScoped<IntegrationEventBuilder>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentProcessingService>();
builder.Services.AddSingleton<IPaymentProviderStrategy, RandomPaymentProviderStrategy>();
builder.Services.AddSingleton<IPaymentProviderStrategy, AlwaysApprovePaymentProviderStrategy>();
builder.Services.AddSingleton<IPaymentProviderStrategy, AlwaysTemporaryFailPaymentProviderStrategy>();
builder.Services.AddSingleton<IPaymentProviderStrategy, AlwaysRejectPaymentProviderStrategy>();
builder.Services.AddSingleton<IPaymentProviderStrategy, TimeoutPaymentProviderStrategy>();
builder.Services.AddSingleton<FakePaymentProviderService>();
builder.Services.AddHttpClient<IPaymentProviderClient, PaymentProviderClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FakePaymentProviderOptions>>();
    client.BaseAddress = new Uri(options.Value.BaseUrl);
});

builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<OrderCreatedConsumerWorker>();
builder.Services.AddHostedService<PaymentRetryWorker>();

builder.Services.AddHealthChecks();

builder.WebHost.UseUrls("http://localhost:8081");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

    logger.LogInformation("Applying pending EF Core migrations before background workers start");
    await dbContext.Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapOrdersEndpoints();
app.MapMessagingDebugEndpoints();
app.MapPaymentsEndpoints();
app.MapFakePaymentProviderEndpoints();

app.Run();
