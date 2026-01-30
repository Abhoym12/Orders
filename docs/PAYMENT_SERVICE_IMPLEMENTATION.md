# Payment Service Implementation Guide

## Overview

Implement a new PaymentService microservice to handle payment transactions and modify OrderProcessor to validate payments before order state transitions.

## Architecture Integration

```
ApiGateway → PaymentService (new)
             ├── PaymentService.Api
             ├── PaymentService.Manager
             ├── PaymentService.Engine
             └── PaymentService.DataAccess

Events: order-created → PaymentService → payment-completed → OrderProcessor
```

---

## Phase 1: Database Schema Changes

### 1.1 Create PaymentsDb Database

Create file: `scripts/05-create-payments-database.sql`

```sql
-- Create PaymentsDb
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PaymentsDb')
BEGIN
    CREATE DATABASE PaymentsDb;
END
GO

USE PaymentsDb;
GO

-- Transactions table
CREATE TABLE Transactions (
    TransactionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OrderId UNIQUEIDENTIFIER NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Date DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, COMPLETED, FAILED, REFUNDED
    PaymentMethod NVARCHAR(50) NULL, -- CREDIT_CARD, DEBIT_CARD, PAYPAL, etc.
    TransactionReference NVARCHAR(255) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT CK_Transactions_Amount CHECK (Amount >= 0)
);
GO

-- Indexes for performance
CREATE INDEX IX_Transactions_OrderId ON Transactions(OrderId);
CREATE INDEX IX_Transactions_Status ON Transactions(Status);
CREATE INDEX IX_Transactions_Date ON Transactions(Date);
GO
```

### 1.2 Update Orders Table

Create file: `scripts/06-alter-orders-add-payment.sql`

```sql
USE OrdersDb;
GO

-- Add AmountPaid column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'AmountPaid')
BEGIN
    ALTER TABLE Orders
    ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0.0;
END
GO

-- Add TotalAmount column if not exists (required for comparison)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'TotalAmount')
BEGIN
    ALTER TABLE Orders
    ADD TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0.0;
END
GO

-- Add check constraint
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Orders_AmountPaid')
BEGIN
    ALTER TABLE Orders
    ADD CONSTRAINT CK_Orders_AmountPaid CHECK (AmountPaid >= 0 AND AmountPaid <= TotalAmount);
END
GO

-- Create index for payment queries
CREATE INDEX IX_Orders_AmountPaid ON Orders(AmountPaid);
GO
```

---

## Phase 2: Shared Contracts (Events & DTOs)

### 2.1 Payment Events

Create file: `src/Shared/Contracts/Shared.Contracts/Events/PaymentEvents.cs`

```csharp
namespace Shared.Contracts.Events;

public record PaymentInitiatedEvent(
    Guid TransactionId,
    Guid OrderId,
    decimal Amount,
    string PaymentMethod,
    DateTime InitiatedAt
);

public record PaymentCompletedEvent(
    Guid TransactionId,
    Guid OrderId,
    decimal Amount,
    string TransactionReference,
    DateTime CompletedAt
);

public record PaymentFailedEvent(
    Guid TransactionId,
    Guid OrderId,
    decimal Amount,
    string Reason,
    DateTime FailedAt
);

public record PaymentRefundedEvent(
    Guid TransactionId,
    Guid OrderId,
    decimal RefundAmount,
    string Reason,
    DateTime RefundedAt
);
```

### 2.2 Payment DTOs

Create file: `src/Shared/Contracts/Shared.Contracts/Dtos/PaymentDtos.cs`

```csharp
namespace Shared.Contracts.Dtos;

public record CreatePaymentRequest(
    Guid OrderId,
    decimal Amount,
    string PaymentMethod,
    string? CardNumber = null,
    string? CardHolderName = null,
    string? ExpiryDate = null,
    string? Cvv = null
);

public record PaymentResponse(
    Guid TransactionId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? TransactionReference,
    DateTime Date
);

public record TransactionResponse(
    Guid TransactionId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string PaymentMethod,
    string? TransactionReference,
    DateTime Date,
    DateTime CreatedAt
);
```

---

## Phase 3: PaymentService Implementation

### 3.1 Project Structure

Create the following project structure:

```
src/PaymentService/
├── Dockerfile
└── PaymentService/
    ├── PaymentService.Api/
    │   ├── PaymentService.Api.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   └── Controllers/
    │       └── PaymentsController.cs
    ├── PaymentService.Manager/
    │   ├── PaymentService.Manager.csproj
    │   ├── Commands/
    │   │   ├── CreatePaymentCommand.cs
    │   │   └── CreatePaymentCommandHandler.cs
    │   ├── Queries/
    │   │   ├── GetTransactionByOrderIdQuery.cs
    │   │   └── GetTransactionByOrderIdQueryHandler.cs
    │   └── Validators/
    │       └── CreatePaymentCommandValidator.cs
    ├── PaymentService.Engine/
    │   ├── PaymentService.Engine.csproj
    │   ├── IPaymentEngine.cs
    │   └── PaymentEngine.cs
    └── PaymentService.DataAccess/
        ├── PaymentService.DataAccess.csproj
        ├── Models/
        │   └── Transaction.cs
        ├── Repositories/
        │   ├── ITransactionRepository.cs
        │   └── TransactionRepository.cs
        └── Kafka/
            ├── IPaymentEventProducer.cs
            └── PaymentEventProducer.cs
```

### 3.2 PaymentService.DataAccess Layer

#### Transaction Model

`src/PaymentService/PaymentService.DataAccess/Models/Transaction.cs`

```csharp
namespace PaymentService.DataAccess.Models;

public class Transaction
{
    public Guid TransactionId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, FAILED, REFUNDED
    public string? PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### Transaction Repository Interface

`src/PaymentService/PaymentService.DataAccess/Repositories/ITransactionRepository.cs`

```csharp
using PaymentService.DataAccess.Models;

namespace PaymentService.DataAccess.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct);
    Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken ct);
    Task<Transaction?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<IEnumerable<Transaction>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken ct);
    Task UpdateStatusAsync(Guid transactionId, string status, string? transactionReference, CancellationToken ct);
}
```

#### Transaction Repository Implementation

`src/PaymentService/PaymentService.DataAccess/Repositories/TransactionRepository.cs`

```csharp
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PaymentService.DataAccess.Models;

namespace PaymentService.DataAccess.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly string _connectionString;

    public TransactionRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PaymentsDb")
            ?? throw new InvalidOperationException("PaymentsDb connection string not found");
    }

    public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO Transactions (TransactionId, OrderId, Amount, Date, Status, PaymentMethod, TransactionReference, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.*
            VALUES (@TransactionId, @OrderId, @Amount, @Date, @Status, @PaymentMethod, @TransactionReference, @CreatedAt, @UpdatedAt)";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<Transaction>(sql, transaction);
    }

    public async Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken ct)
    {
        const string sql = "SELECT * FROM Transactions WHERE TransactionId = @TransactionId";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Transaction>(sql, new { TransactionId = transactionId });
    }

    public async Task<Transaction?> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        const string sql = "SELECT TOP 1 * FROM Transactions WHERE OrderId = @OrderId ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Transaction>(sql, new { OrderId = orderId });
    }

    public async Task<IEnumerable<Transaction>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken ct)
    {
        const string sql = "SELECT * FROM Transactions WHERE OrderId IN @OrderIds";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Transaction>(sql, new { OrderIds = orderIds });
    }

    public async Task UpdateStatusAsync(Guid transactionId, string status, string? transactionReference, CancellationToken ct)
    {
        const string sql = @"
            UPDATE Transactions
            SET Status = @Status,
                TransactionReference = @TransactionReference,
                UpdatedAt = GETUTCDATE()
            WHERE TransactionId = @TransactionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { TransactionId = transactionId, Status = status, TransactionReference = transactionReference });
    }
}
```

#### Payment Event Producer

`src/PaymentService/PaymentService.DataAccess/Kafka/PaymentEventProducer.cs`

```csharp
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace PaymentService.DataAccess.Kafka;

public interface IPaymentEventProducer
{
    Task PublishAsync<TEvent>(string topic, TEvent eventData, CancellationToken ct) where TEvent : class;
}

public class PaymentEventProducer : IPaymentEventProducer
{
    private readonly IProducer<string, string> _producer;

    public PaymentEventProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<TEvent>(string topic, TEvent eventData, CancellationToken ct) where TEvent : class
    {
        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = JsonSerializer.Serialize(eventData)
        };

        await _producer.ProduceAsync(topic, message, ct);
    }
}
```

### 3.3 PaymentService.Engine Layer

#### Payment Engine Interface

`src/PaymentService/PaymentService.Engine/IPaymentEngine.cs`

```csharp
using PaymentService.DataAccess.Models;
using Shared.Contracts.Dtos;

namespace PaymentService.Engine;

public interface IPaymentEngine
{
    Transaction CreateTransaction(CreatePaymentRequest request);
    void ValidatePayment(CreatePaymentRequest request);
    Task<(bool IsSuccess, string TransactionReference, string? ErrorReason)> ProcessPaymentAsync(
        CreatePaymentRequest request, CancellationToken ct);
}
```

#### Payment Engine Implementation

`src/PaymentService/PaymentService.Engine/PaymentEngine.cs`

```csharp
using PaymentService.DataAccess.Models;
using Shared.Contracts.Dtos;

namespace PaymentService.Engine;

public class PaymentEngine : IPaymentEngine
{
    public Transaction CreateTransaction(CreatePaymentRequest request)
    {
        ValidatePayment(request);

        return new Transaction
        {
            TransactionId = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = request.Amount,
            Date = DateTime.UtcNow,
            Status = "PENDING",
            PaymentMethod = request.PaymentMethod,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ValidatePayment(CreatePaymentRequest request)
    {
        if (request.OrderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty", nameof(request.OrderId));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(request.Amount));

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException("PaymentMethod is required", nameof(request.PaymentMethod));

        var validPaymentMethods = new[] { "CREDIT_CARD", "DEBIT_CARD", "PAYPAL", "BANK_TRANSFER" };
        if (!validPaymentMethods.Contains(request.PaymentMethod.ToUpper()))
            throw new ArgumentException($"Invalid payment method. Supported methods: {string.Join(", ", validPaymentMethods)}",
                nameof(request.PaymentMethod));
    }

    public async Task<(bool IsSuccess, string TransactionReference, string? ErrorReason)> ProcessPaymentAsync(
        CreatePaymentRequest request, CancellationToken ct)
    {
        // Simulate payment gateway integration
        // In production, integrate with Stripe, PayPal, etc.
        await Task.Delay(1000, ct); // Simulate network call

        // Simulate 95% success rate for demo purposes
        var random = new Random();
        var isSuccess = random.Next(100) < 95;

        if (isSuccess)
        {
            var transactionRef = $"TXN-{Guid.NewGuid().ToString()[..8].ToUpper()}";
            return (true, transactionRef, null);
        }
        else
        {
            var errorReasons = new[] { "Insufficient funds", "Card declined", "Payment timeout", "Invalid card details" };
            var errorReason = errorReasons[random.Next(errorReasons.Length)];
            return (false, string.Empty, errorReason);
        }
    }
}
```

### 3.4 PaymentService.Manager Layer

#### Create Payment Command

`src/PaymentService/PaymentService.Manager/Commands/CreatePaymentCommand.cs`

```csharp
using MediatR;
using Shared.Contracts.Dtos;

namespace PaymentService.Manager.Commands;

public record CreatePaymentCommand(CreatePaymentRequest Request) : IRequest<PaymentResponse>;
```

#### Create Payment Command Handler

`src/PaymentService/PaymentService.Manager/Commands/CreatePaymentCommandHandler.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.DataAccess.Kafka;
using PaymentService.DataAccess.Repositories;
using PaymentService.Engine;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;

namespace PaymentService.Manager.Commands;

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentResponse>
{
    private readonly IPaymentEngine _paymentEngine;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentEventProducer _eventProducer;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    public CreatePaymentCommandHandler(
        IPaymentEngine paymentEngine,
        ITransactionRepository transactionRepository,
        IPaymentEventProducer eventProducer,
        ILogger<CreatePaymentCommandHandler> logger)
    {
        _paymentEngine = paymentEngine;
        _transactionRepository = transactionRepository;
        _eventProducer = eventProducer;
        _logger = logger;
    }

    public async Task<PaymentResponse> Handle(CreatePaymentCommand command, CancellationToken ct)
    {
        var request = command.Request;

        // Engine validates and creates transaction
        var transaction = _paymentEngine.CreateTransaction(request);

        // Persist transaction
        await _transactionRepository.AddAsync(transaction, ct);

        // Publish payment initiated event
        await _eventProducer.PublishAsync("payment-initiated",
            new PaymentInitiatedEvent(
                transaction.TransactionId,
                transaction.OrderId,
                transaction.Amount,
                transaction.PaymentMethod ?? "UNKNOWN",
                transaction.Date
            ), ct);

        _logger.LogInformation("Payment initiated for Order {OrderId}, Transaction {TransactionId}",
            transaction.OrderId, transaction.TransactionId);

        // Process payment with external gateway
        var (isSuccess, transactionRef, errorReason) = await _paymentEngine.ProcessPaymentAsync(request, ct);

        if (isSuccess)
        {
            // Update transaction status
            await _transactionRepository.UpdateStatusAsync(transaction.TransactionId, "COMPLETED", transactionRef, ct);

            // Publish payment completed event
            await _eventProducer.PublishAsync("payment-completed",
                new PaymentCompletedEvent(
                    transaction.TransactionId,
                    transaction.OrderId,
                    transaction.Amount,
                    transactionRef,
                    DateTime.UtcNow
                ), ct);

            _logger.LogInformation("Payment completed for Order {OrderId}, Reference {Reference}",
                transaction.OrderId, transactionRef);

            return new PaymentResponse(
                transaction.TransactionId,
                transaction.OrderId,
                transaction.Amount,
                "COMPLETED",
                transactionRef,
                transaction.Date
            );
        }
        else
        {
            // Update transaction status
            await _transactionRepository.UpdateStatusAsync(transaction.TransactionId, "FAILED", null, ct);

            // Publish payment failed event
            await _eventProducer.PublishAsync("payment-failed",
                new PaymentFailedEvent(
                    transaction.TransactionId,
                    transaction.OrderId,
                    transaction.Amount,
                    errorReason ?? "Unknown error",
                    DateTime.UtcNow
                ), ct);

            _logger.LogWarning("Payment failed for Order {OrderId}, Reason: {Reason}",
                transaction.OrderId, errorReason);

            throw new InvalidOperationException($"Payment failed: {errorReason}");
        }
    }
}
```

#### Get Transaction Query

`src/PaymentService/PaymentService.Manager/Queries/GetTransactionByOrderIdQuery.cs`

```csharp
using MediatR;
using Shared.Contracts.Dtos;

namespace PaymentService.Manager.Queries;

public record GetTransactionByOrderIdQuery(Guid OrderId) : IRequest<TransactionResponse?>;
```

#### Get Transaction Query Handler

`src/PaymentService/PaymentService.Manager/Queries/GetTransactionByOrderIdQueryHandler.cs`

```csharp
using MediatR;
using PaymentService.DataAccess.Repositories;
using Shared.Contracts.Dtos;

namespace PaymentService.Manager.Queries;

public class GetTransactionByOrderIdQueryHandler : IRequestHandler<GetTransactionByOrderIdQuery, TransactionResponse?>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetTransactionByOrderIdQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<TransactionResponse?> Handle(GetTransactionByOrderIdQuery query, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetByOrderIdAsync(query.OrderId, ct);

        if (transaction == null)
            return null;

        return new TransactionResponse(
            transaction.TransactionId,
            transaction.OrderId,
            transaction.Amount,
            transaction.Status,
            transaction.PaymentMethod ?? "UNKNOWN",
            transaction.TransactionReference,
            transaction.Date,
            transaction.CreatedAt
        );
    }
}
```

#### Command Validator

`src/PaymentService/PaymentService.Manager/Validators/CreatePaymentCommandValidator.cs`

```csharp
using FluentValidation;
using PaymentService.Manager.Commands;

namespace PaymentService.Manager.Validators;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Request.OrderId)
            .NotEmpty().WithMessage("OrderId is required");

        RuleFor(x => x.Request.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Request.PaymentMethod)
            .NotEmpty().WithMessage("PaymentMethod is required")
            .Must(BeValidPaymentMethod).WithMessage("Invalid payment method");
    }

    private bool BeValidPaymentMethod(string paymentMethod)
    {
        var validMethods = new[] { "CREDIT_CARD", "DEBIT_CARD", "PAYPAL", "BANK_TRANSFER" };
        return validMethods.Contains(paymentMethod.ToUpper());
    }
}
```

### 3.5 PaymentService.Api Layer

#### Payments Controller

`src/PaymentService/PaymentService.Api/Controllers/PaymentsController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Manager.Commands;
using PaymentService.Manager.Queries;
using Shared.Contracts.Dtos;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new CreatePaymentCommand(request);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<TransactionResponse>> GetTransactionByOrderId(
        Guid orderId,
        CancellationToken ct)
    {
        var query = new GetTransactionByOrderIdQuery(orderId);
        var result = await _mediator.Send(query, ct);

        if (result == null)
            return NotFound(new ProblemDetails { Detail = $"Transaction for Order {orderId} not found" });

        return Ok(result);
    }
}
```

#### Program.cs

`src/PaymentService/PaymentService.Api/Program.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentService.DataAccess.Kafka;
using PaymentService.DataAccess.Repositories;
using PaymentService.Engine;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PaymentService.Manager.Commands.CreatePaymentCommand).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(PaymentService.Manager.Commands.CreatePaymentCommand).Assembly);

// Register services
builder.Services.AddScoped<IPaymentEngine, PaymentEngine>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<IPaymentEventProducer, PaymentEventProducer>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

#### appsettings.json

`src/PaymentService/PaymentService.Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "PaymentsDb": "Server=localhost,1433;Database=PaymentsDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "OrderPlatform",
    "Audience": "OrderPlatformUsers"
  }
}
```

### 3.6 .csproj Files

#### PaymentService.Api.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.*" />
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PaymentService.Manager\PaymentService.Manager.csproj" />
    <ProjectReference Include="..\PaymentService.Engine\PaymentService.Engine.csproj" />
    <ProjectReference Include="..\PaymentService.DataAccess\PaymentService.DataAccess.csproj" />
    <ProjectReference Include="..\..\Shared\Contracts\Shared.Contracts\Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

#### PaymentService.Manager.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.*" />
    <PackageReference Include="FluentValidation" Version="11.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PaymentService.Engine\PaymentService.Engine.csproj" />
    <ProjectReference Include="..\PaymentService.DataAccess\PaymentService.DataAccess.csproj" />
    <ProjectReference Include="..\..\Shared\Contracts\Shared.Contracts\Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

#### PaymentService.Engine.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\PaymentService.DataAccess\PaymentService.DataAccess.csproj" />
    <ProjectReference Include="..\..\Shared\Contracts\Shared.Contracts\Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

#### PaymentService.DataAccess.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.*" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />
    <PackageReference Include="Confluent.Kafka" Version="2.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Contracts\Shared.Contracts\Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

---

## Phase 4: Update OrderService

### 4.1 Update Order Model

Update `src/OrderService/OrderService.DataAccess/OrderService.DataAccess/Models/Order.cs`

Add properties:

```csharp
public decimal TotalAmount { get; set; }
public decimal AmountPaid { get; set; }
```

### 4.2 Update Order Repository

Update `src/OrderService/OrderService.DataAccess/OrderService.DataAccess/Repositories/OrderRepository.cs`

1. Include `TotalAmount` and `AmountPaid` in INSERT statements
2. Add new method:

```csharp
public async Task UpdateAmountPaidAsync(Guid orderId, decimal amountPaid, CancellationToken ct)
{
    const string sql = @"
        UPDATE Orders
        SET AmountPaid = @AmountPaid, UpdatedAt = GETUTCDATE()
        WHERE OrderId = @OrderId";

    await using var connection = new SqlConnection(_connectionString);
    await connection.ExecuteAsync(sql, new { OrderId = orderId, AmountPaid = amountPaid });
}
```

Add to `IOrderRepository` interface as well.

### 4.3 Create Payment Completed Event Consumer

Create file: `src/OrderService/OrderService.DataAccess/OrderService.DataAccess/Kafka/PaymentCompletedEventConsumer.cs`

```csharp
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Repositories;
using Shared.Contracts.Events;
using System.Text.Json;

namespace OrderService.DataAccess.Kafka;

public class PaymentCompletedEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<PaymentCompletedEventConsumer> _logger;

    public PaymentCompletedEventConsumer(
        IConfiguration configuration,
        IOrderRepository orderRepository,
        ILogger<PaymentCompletedEventConsumer> logger)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "order-service-payment-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _orderRepository = orderRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("payment-completed");
        _logger.LogInformation("PaymentCompletedEventConsumer started listening to payment-completed topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                var paymentEvent = JsonSerializer.Deserialize<PaymentCompletedEvent>(consumeResult.Message.Value);

                if (paymentEvent != null)
                {
                    await _orderRepository.UpdateAmountPaidAsync(paymentEvent.OrderId, paymentEvent.Amount, stoppingToken);
                    _logger.LogInformation("Updated AmountPaid for Order {OrderId} to {Amount}",
                        paymentEvent.OrderId, paymentEvent.Amount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment-completed event");
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        base.Dispose();
    }
}
```

Register in `OrderService.Api/Program.cs`:

```csharp
builder.Services.AddHostedService<PaymentCompletedEventConsumer>();
```

---

## Phase 5: Update OrderProcessor

### 5.1 Modify OrderProcessorWorker Logic

Update `src/OrderProcessor/OrderProcessor/OrderProcessorWorker.cs`

Modify the `ExecuteAsync` method to check payment status:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("OrderProcessorWorker started at: {time}", DateTimeOffset.UtcNow);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await ProcessPendingOrdersAsync(stoppingToken);
            await Task.Delay(
                TimeSpan.FromMinutes(_settings.PollingIntervalMinutes),
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OrderProcessorWorker");
        }
    }
}

private async Task ProcessPendingOrdersAsync(CancellationToken ct)
{
    const string sql = @"
        SELECT TOP (@BatchSize) *
        FROM Orders
        WHERE Status = 'PENDING'
        AND AmountPaid >= TotalAmount
        AND TotalAmount > 0
        ORDER BY CreatedAt ASC";

    await using var connection = new SqlConnection(_connectionString);
    var pendingOrders = await connection.QueryAsync<Order>(sql, new { BatchSize = _settings.BatchSize });

    _logger.LogInformation("Found {Count} pending orders with completed payments to process", pendingOrders.Count());

    await Parallel.ForEachAsync(pendingOrders, ct, async (order, token) =>
    {
        try
        {
            // Update order status to PROCESSING
            const string updateSql = @"
                UPDATE Orders
                SET Status = 'PROCESSING', UpdatedAt = GETUTCDATE()
                WHERE OrderId = @OrderId AND Status = 'PENDING'";

            await connection.ExecuteAsync(updateSql, new { OrderId = order.OrderId });

            _logger.LogInformation("Order {OrderId} moved from PENDING to PROCESSING (Payment verified: {AmountPaid}/{TotalAmount})",
                order.OrderId, order.AmountPaid, order.TotalAmount);

            // Publish order-status-changed event
            var statusEvent = new OrderStatusChangedEvent(
                order.OrderId,
                "PENDING",
                "PROCESSING",
                DateTime.UtcNow
            );

            await PublishEventAsync("order-status-changed", statusEvent, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.OrderId);
        }
    });
}

private async Task PublishEventAsync<TEvent>(string topic, TEvent eventData, CancellationToken ct)
{
    var config = new ProducerConfig { BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092" };
    using var producer = new ProducerBuilder<string, string>(config).Build();

    var message = new Message<string, string>
    {
        Key = Guid.NewGuid().ToString(),
        Value = JsonSerializer.Serialize(eventData)
    };

    await producer.ProduceAsync(topic, message, ct);
}
```

Add Order model reference at the top if not present:

```csharp
private class Order
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## Phase 6: Infrastructure Updates

### 6.1 Update docker-compose.yml

Add PaymentService and SQL Server database:

```yaml
payment-service:
  build:
    context: ./src/PaymentService
    dockerfile: Dockerfile
  container_name: payment-service
  ports:
    - "5003:8080"
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - ConnectionStrings__PaymentsDb=Server=sql-server,1433;Database=PaymentsDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;
    - Kafka__BootstrapServers=kafka:9092
    - Jwt__Key=YourSuperSecretKeyThatIsAtLeast32CharactersLong!
    - Jwt__Issuer=OrderPlatform
    - Jwt__Audience=OrderPlatformUsers
  depends_on:
    - sql-server
    - kafka
  networks:
    - order-platform-network
```

Update sql-server volumes to include new database:

```yaml
sql-server:
  # ... existing config
  volumes:
    - sql-data:/var/opt/mssql
    - ./scripts:/docker-entrypoint-initdb.d
```

### 6.2 Update Ocelot Configuration

Add payment routes to `src/ApiGateway/ApiGateway/ocelot.json`:

```json
{
  "DownstreamPathTemplate": "/api/payments",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "payment-service",
      "Port": 8080
    }
  ],
  "UpstreamPathTemplate": "/api/payments",
  "UpstreamHttpMethod": [ "POST" ],
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer"
  }
},
{
  "DownstreamPathTemplate": "/api/payments/order/{orderId}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "payment-service",
      "Port": 8080
    }
  ],
  "UpstreamPathTemplate": "/api/payments/order/{orderId}",
  "UpstreamHttpMethod": [ "GET" ],
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer"
  }
}
```

### 6.3 Dockerfile for PaymentService

Create `src/PaymentService/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["PaymentService/PaymentService.Api/PaymentService.Api.csproj", "PaymentService.Api/"]
COPY ["PaymentService/PaymentService.Manager/PaymentService.Manager.csproj", "PaymentService.Manager/"]
COPY ["PaymentService/PaymentService.Engine/PaymentService.Engine.csproj", "PaymentService.Engine/"]
COPY ["PaymentService/PaymentService.DataAccess/PaymentService.DataAccess.csproj", "PaymentService.DataAccess/"]
COPY ["../Shared/Contracts/Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]

# Restore dependencies
RUN dotnet restore "PaymentService.Api/PaymentService.Api.csproj"

# Copy all source files
COPY PaymentService/ .
COPY ../Shared/Contracts/Shared.Contracts/ Shared.Contracts/

# Build
WORKDIR "/src/PaymentService.Api"
RUN dotnet build "PaymentService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
```

---

## Phase 7: Kafka Topics

Add new topics to your Kafka setup. If using docker-compose, ensure these topics are created:

```
payment-initiated
payment-completed
payment-failed
payment-refunded
```

---

## Phase 8: Testing

### 8.1 Unit Tests for PaymentService.Engine

Create `tests/PaymentService.Engine.Tests/PaymentEngineTests.cs`:

```csharp
using PaymentService.Engine;
using Shared.Contracts.Dtos;
using Xunit;

namespace PaymentService.Engine.Tests;

public class PaymentEngineTests
{
    private readonly PaymentEngine _engine;

    public PaymentEngineTests()
    {
        _engine = new PaymentEngine();
    }

    [Fact]
    public void CreateTransaction_ValidRequest_ReturnsTransaction()
    {
        // Arrange
        var request = new CreatePaymentRequest(
            OrderId: Guid.NewGuid(),
            Amount: 100.00m,
            PaymentMethod: "CREDIT_CARD"
        );

        // Act
        var transaction = _engine.CreateTransaction(request);

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.TransactionId);
        Assert.Equal(request.OrderId, transaction.OrderId);
        Assert.Equal(request.Amount, transaction.Amount);
        Assert.Equal("PENDING", transaction.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ValidatePayment_InvalidAmount_ThrowsException(decimal amount)
    {
        // Arrange
        var request = new CreatePaymentRequest(
            OrderId: Guid.NewGuid(),
            Amount: amount,
            PaymentMethod: "CREDIT_CARD"
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _engine.ValidatePayment(request));
    }

    [Fact]
    public void ValidatePayment_InvalidPaymentMethod_ThrowsException()
    {
        // Arrange
        var request = new CreatePaymentRequest(
            OrderId: Guid.NewGuid(),
            Amount: 100.00m,
            PaymentMethod: "INVALID_METHOD"
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _engine.ValidatePayment(request));
    }
}
```

### 8.2 Integration Test Workflow

1. Create Order via OrderService API → Order created with Status=PENDING, AmountPaid=0
2. Create Payment via PaymentService API → Payment processed, payment-completed event published
3. OrderService consumes payment-completed event → Updates AmountPaid
4. OrderProcessor polls pending orders → Finds order with AmountPaid >= TotalAmount → Moves to PROCESSING

---

## Phase 9: Postman Collection Updates

Add new requests to `docs/postman/OrderPlatform.postman_collection.json`:

### Create Payment Request

```json
{
  "name": "Create Payment",
  "request": {
    "method": "POST",
    "header": [
      {
        "key": "Authorization",
        "value": "Bearer {{access_token}}"
      }
    ],
    "body": {
      "mode": "raw",
      "raw": "{\n  \"orderId\": \"{{order_id}}\",\n  \"amount\": 150.00,\n  \"paymentMethod\": \"CREDIT_CARD\",\n  \"cardNumber\": \"4532123456789012\",\n  \"cardHolderName\": \"John Doe\",\n  \"expiryDate\": \"12/25\",\n  \"cvv\": \"123\"\n}",
      "options": {
        "raw": {
          "language": "json"
        }
      }
    },
    "url": {
      "raw": "{{base_url}}/api/payments",
      "host": ["{{base_url}}"],
      "path": ["api", "payments"]
    }
  }
}
```

### Get Transaction by Order ID

```json
{
  "name": "Get Transaction by Order ID",
  "request": {
    "method": "GET",
    "header": [
      {
        "key": "Authorization",
        "value": "Bearer {{access_token}}"
      }
    ],
    "url": {
      "raw": "{{base_url}}/api/payments/order/{{order_id}}",
      "host": ["{{base_url}}"],
      "path": ["api", "payments", "order", "{{order_id}}"]
    }
  }
}
```

---

## Phase 10: Deployment Checklist

- [ ] Run SQL scripts in order (05-create-payments-database.sql, 06-alter-orders-add-payment.sql)
- [ ] Build all PaymentService projects
- [ ] Add PaymentService projects to solution file
- [ ] Update docker-compose.yml with PaymentService
- [ ] Create Kafka topics (payment-initiated, payment-completed, payment-failed)
- [ ] Update API Gateway ocelot.json configuration
- [ ] Register PaymentCompletedEventConsumer in OrderService
- [ ] Update OrderProcessor logic for payment validation
- [ ] Run tests for PaymentService.Engine and PaymentService.Manager
- [ ] Test end-to-end flow: Order → Payment → Status Transition
- [ ] Update documentation (ARCHITECTURE.md, README.md)
- [ ] Update Postman collection with payment endpoints

---

## Expected Workflow After Implementation

1. **User creates order** → Order created with `Status=PENDING`, `TotalAmount=X`, `AmountPaid=0`
2. **User initiates payment** → POST `/api/payments` with OrderId and Amount
3. **PaymentService processes payment** → Creates transaction, processes with gateway, publishes `payment-completed` event
4. **OrderService consumes event** → Updates `AmountPaid` for the order
5. **OrderProcessor polls** → Finds orders where `Status=PENDING` AND `AmountPaid >= TotalAmount`
6. **OrderProcessor transitions** → Updates order to `Status=PROCESSING`
7. **Subsequent workers** → Process PROCESSING → SHIPPED → DELIVERED

---

## Kafka Event Flow Diagram

```
OrderService → order-created → [Notification/Logging Services]
                ↓
PaymentService receives OrderId from client
                ↓
PaymentService → payment-completed → OrderService (updates AmountPaid)
                ↓
OrderProcessor → Checks AmountPaid >= TotalAmount → Moves PENDING → PROCESSING
                ↓
OrderProcessor → order-status-changed → [Downstream Services]
```

---

## Implementation Notes

1. **Security**: Ensure payment card details are encrypted in transit (HTTPS) and never stored in database
2. **Idempotency**: Consider adding transaction deduplication (check if payment already exists for OrderId)
3. **Retry Logic**: Implement retry with exponential backoff for payment gateway calls
4. **Monitoring**: Add logging for payment success/failure rates
5. **Testing**: Use mock payment gateway in development (current implementation simulates 95% success)
6. **Production**: Replace `PaymentEngine.ProcessPaymentAsync` with actual payment gateway SDK (Stripe, PayPal, etc.)

---

## Summary

This implementation adds a complete payment microservice following your N-Tier architecture pattern:

✅ **PaymentsDb** with Transactions table
✅ **AmountPaid** field in Orders table
✅ **PaymentService** with Api → Manager → Engine → DataAccess layers
✅ **Event-driven** communication via Kafka
✅ **OrderProcessor** payment validation before state transition
✅ **JWT authentication** on all payment endpoints
✅ **Dapper** for all data access
✅ **MediatR** for CQRS in Manager layer
✅ **FluentValidation** for command validation
✅ **Unit tests** structure for Engine layer

The system now ensures orders only move to PROCESSING status after successful payment completion.
