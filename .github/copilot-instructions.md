# Copilot Instructions - Order Processing Platform

## Project Overview

E-commerce order processing platform using .NET 8, microservices, N-Tier architecture, and event-driven design. Target: 50,000+ concurrent users.

## Architecture

### Solution Structure

```
/order-platform/src
├── ApiGateway/                    # Ocelot-based gateway, JWT validation
├── IdentityService/               # Custom Identity + JWT issuance (Dapper)
├── OrderService/
│   ├── OrderService.Api/          # Controllers (thin, delegates to Manager)
│   ├── OrderService.Manager/      # Business orchestration, MediatR handlers
│   ├── OrderService.Engine/       # Domain logic, rules, validations
│   └── OrderService.DataAccess/   # Dapper repositories, Kafka producers
├── OrderProcessor/                # Background worker for state transitions
└── Shared/
    ├── Contracts/                 # Shared DTOs, events
    └── Messaging/                 # Kafka abstractions
```

### N-Tier Dependency Flow (Strict)

```
Controller → Manager → Engine → DataAccess
```

- **Controller**: HTTP handling only, delegates immediately to Manager
- **Manager**: Orchestrates workflows, MediatR handlers, calls Engine for logic
- **Engine**: Pure domain logic, validation rules, state transitions
- **DataAccess**: Dapper, Kafka, Redis - no business logic

### Required NuGet Packages

```xml
<!-- Core -->
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />

<!-- Data Access (Dapper) -->
<PackageReference Include="Dapper" Version="2.*" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />

<!-- Infrastructure -->
<PackageReference Include="Confluent.Kafka" Version="2.*" />
<PackageReference Include="StackExchange.Redis" Version="2.*" />

<!-- API Gateway -->
<PackageReference Include="Ocelot" Version="23.*" />

<!-- Auth -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.*" />

<!-- Testing -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
```

## Naming Conventions (C# Standard)

- **Classes/Interfaces**: PascalCase (`OrderManager`, `IOrderEngine`)
- **Methods**: PascalCase (`CreateOrderAsync`, `ValidateOrderState`)
- **Variables/Parameters**: camelCase (`orderId`, `cancellationToken`)
- **Constants**: PascalCase (`MaxOrderItems`)
- **Private fields**: `_camelCase` (`_orderRepository`)
- **Async methods**: Suffix with `Async` (`GetOrderByIdAsync`)

### Layer Naming Pattern

| Layer      | Suffix       | Example            |
| ---------- | ------------ | ------------------ |
| Controller | `Controller` | `OrdersController` |
| Manager    | `Manager`    | `OrderManager`     |
| Engine     | `Engine`     | `OrderEngine`      |
| Repository | `Repository` | `OrderRepository`  |

## Core Patterns

### CQRS with MediatR (in Manager Layer)

```csharp
// Manager handles orchestration, Engine handles domain logic
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IOrderEngine _orderEngine;
    private readonly IOrderRepository _orderRepository;
    private readonly IKafkaProducer _kafkaProducer;

    public async Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = _orderEngine.CreateOrder(request);  // Engine validates
        await _orderRepository.AddAsync(order, ct);     // DataAccess persists
        await _kafkaProducer.PublishAsync(new OrderCreatedEvent(order), ct);
        return order.ToResponse();
    }
}
```

### Domain Rules (Enforce in Engine Layer Only)

Order lifecycle: `PENDING → PROCESSING → SHIPPED → DELIVERED`

- Only `PENDING` orders can be cancelled
- State transitions validated in `OrderEngine.ValidateStateTransition()`

### Event-Driven Communication (Kafka)

Topics: `order-created`, `order-status-changed`, `order-cancelled`

- **No direct service-to-service HTTP calls**
- Use `Confluent.Kafka` with `IKafkaProducer`/`IKafkaConsumer` abstractions

## Authentication (JWT + Refresh Tokens)

```
Access Token:  15 minutes expiry
Refresh Token: 7 days expiry (stored in DB, single-use rotation)
```

- API Gateway validates JWT on all requests
- IdentityService issues tokens via `/auth/login`, `/auth/refresh`
- Roles: `Customer`, `Admin`

## Database (SQL Server)

```sql
CREATE INDEX IX_Orders_Status ON Orders(Status);
CREATE INDEX IX_Orders_UserId ON Orders(UserId);
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
```

Use async Dapper operations exclusively (`QueryAsync`, `QueryFirstOrDefaultAsync`).

## API Conventions

| Endpoint              | Method | Auth                  |
| --------------------- | ------ | --------------------- |
| `/orders`             | POST   | JWT (Customer, Admin) |
| `/orders/{id}`        | GET    | JWT                   |
| `/orders?status=`     | GET    | JWT                   |
| `/orders/{id}/cancel` | PUT    | JWT                   |

## Testing Strategy (xUnit)

- **Engine.Tests**: State transitions, validation rules (pure unit tests)
- **Manager.Tests**: Handler orchestration (mock Engine, Repository)
- **Api.Tests**: Auth enforcement, HTTP status codes (WebApplicationFactory)

## OrderProcessor Configuration

```json
// appsettings.json
{
  "OrderProcessor": {
    "PollingIntervalMinutes": 5,
    "BatchSize": 100
  }
}
```

Polling interval must be configurable, not hardcoded.

## Error Handling & Logging

- Use standard exceptions with `try-catch` at Controller/Manager boundaries
- Use built-in `ILogger<T>` for all logging (no Serilog)
- Log at appropriate levels: `Information` for flow, `Warning` for recoverable issues, `Error` for failures

```csharp
_logger.LogError(ex, "Failed to create order for user {UserId}", request.UserId);
```

- Return `ProblemDetails` for API error responses (400, 404, 500)

## Docker Compose Services

`sql-server`, `kafka`, `zookeeper`, `redis`, `api-gateway`, `identity-service`, `order-service`, `order-processor`

## Key Constraints

- ❌ No business logic in Controllers or DataAccess
- ❌ No synchronous service-to-service calls
- ❌ No blocking I/O operations
- ✅ All operations async/await
- ✅ Use `Parallel.ForEachAsync` in OrderProcessor worker
- ✅ Redis caching for order reads (cache-aside, 5 min TTL)
