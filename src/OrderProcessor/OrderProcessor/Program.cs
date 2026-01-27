using OrderProcessor;
using OrderService.DataAccess;
using OrderService.Engine;
using OrderService.Engine.Interfaces;
using Shared.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// Configure settings
builder.Services.Configure<OrderProcessorSettings>(
    builder.Configuration.GetSection("OrderProcessor"));

// Configure Data Access
var connectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("SQL Server connection string is not configured");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string is not configured");

builder.Services.AddDataAccess(connectionString, redisConnectionString);

// Register Engine services
builder.Services.AddScoped<IOrderEngine, OrderEngine>();

// Configure Kafka
builder.Services.AddKafkaMessaging(options =>
{
    options.BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    options.GroupId = builder.Configuration["Kafka:GroupId"] ?? "order-processor";
});

// Add the worker service
builder.Services.AddHostedService<OrderProcessorWorker>();

var host = builder.Build();
host.Run();
