# Order Processing Platform

A scalable e-commerce order processing platform built with .NET 8, microservices architecture, and event-driven design. Designed to handle **50,000+ concurrent users**.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENTS                              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   API GATEWAY (Ocelot)                      │
│              JWT Validation • Rate Limiting • Routing       │
│                        Port: 8080                           │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│    IDENTITY SERVICE     │     │      ORDER SERVICE      │
│       Port: 5178        │     │       Port: 5179        │
│  • User Registration    │     │  • Create/Cancel Orders │
│  • JWT Issuance         │     │  • Order Queries        │
│  • Token Refresh        │     │  • Status Management    │
└─────────────────────────┘     └─────────────────────────┘
              │                         │
              ▼                         ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│   SQL Server (Identity) │     │  SQL Server (Orders)    │
│                         │     │  + Redis Cache          │
└─────────────────────────┘     └─────────────────────────┘
                                        │
                                        ▼ Kafka Events
                                ┌─────────────────────────┐
                                │    ORDER PROCESSOR      │
                                │   (Background Worker)   │
                                └─────────────────────────┘
```

## 🛠️ Technology Stack

| Component   | Technology           |
| ----------- | -------------------- |
| Framework   | .NET 8               |
| API Gateway | Ocelot               |
| Database    | SQL Server           |
| Caching     | Redis                |
| Messaging   | Apache Kafka         |
| ORM         | Dapper               |
| CQRS        | MediatR              |
| Validation  | FluentValidation     |
| Auth        | JWT + Refresh Tokens |
| Testing     | xUnit, FakeItEasy    |

## 📁 Project Structure

```
/Orders
├── src/
│   ├── ApiGateway/                    # Ocelot-based gateway
│   ├── IdentityService/               # Authentication & user management
│   ├── OrderService/
│   │   ├── OrderService.Api/          # HTTP Controllers
│   │   ├── OrderService.Manager/      # MediatR handlers, orchestration
│   │   ├── OrderService.Engine/       # Domain logic, business rules
│   │   └── OrderService.DataAccess/   # Dapper repositories, Kafka
│   ├── OrderProcessor/                # Background worker for state transitions
│   └── Shared/
│       ├── Contracts/                 # Shared DTOs, events
│       └── Messaging/                 # Kafka abstractions
├── tests/
│   ├── OrderService.Engine.Tests/     # Domain logic unit tests
│   └── OrderService.Manager.Tests/    # Handler unit tests
├── docker-compose.yml                 # Full deployment
└── docker-compose.infrastructure.yml  # Local dev infrastructure
```

## 📋 Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/)

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/Abhoym12/Orders.git
cd Orders
```

### 2. Start Infrastructure (Docker)

```bash
docker-compose -f docker-compose.infrastructure.yml up -d
```

This starts:

- **SQL Server** (port 1433)
- **Redis** (port 6379)
- **Kafka** (port 9092)
- **Zookeeper** (port 2181)

### 3. Run the Services

Open separate terminals for each service:

**Terminal 1 - Identity Service:**

```bash
dotnet run --project src/IdentityService/IdentityService
```

**Terminal 2 - Order Service:**

```bash
dotnet run --project src/OrderService/OrderService.Api/OrderService.Api
```

**Terminal 3 - API Gateway:**

```bash
dotnet run --project src/ApiGateway/ApiGateway
```

**Terminal 4 - Order Processor (optional):**

```bash
dotnet run --project src/OrderProcessor/OrderProcessor
```

### 4. Verify Services are Running

| Service          | URL                   |
| ---------------- | --------------------- |
| API Gateway      | http://localhost:8080 |
| Identity Service | http://localhost:5178 |
| Order Service    | http://localhost:5179 |

## 🧪 Testing

### Run Unit Tests

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test tests/OrderService.Engine.Tests
dotnet test tests/OrderService.Manager.Tests
```

### API Testing

#### Option 1: Using cURL

**1. Register a User**

```bash
curl -X POST http://localhost:5178/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "Test123!",
    "firstName": "Test",
    "lastName": "User"
  }'
```

**2. Login to Get Tokens**

```bash
curl -X POST http://localhost:5178/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "Test123!"
  }'
```

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "abc123...",
  "expiresIn": 900
}
```

**3. Create an Order**

```bash
curl -X POST http://localhost:5179/api/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "items": [
      {
        "productId": "11111111-1111-1111-1111-111111111111",
        "productName": "Test Product",
        "quantity": 2,
        "price": 29.99
      }
    ]
  }'
```

**4. Get Order by ID**

```bash
curl -X GET http://localhost:5179/api/orders/{orderId} \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

**5. List Orders by Status**

```bash
curl -X GET "http://localhost:5179/api/orders?status=Pending" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

**6. Cancel an Order**

```bash
curl -X PUT http://localhost:5179/api/orders/{orderId}/cancel \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

**7. Refresh Token**

```bash
curl -X POST http://localhost:5178/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "accessToken": "YOUR_EXPIRED_ACCESS_TOKEN",
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'
```

#### Option 2: Using PowerShell

```powershell
# Register
$register = Invoke-RestMethod -Uri "http://localhost:5178/api/auth/register" -Method POST -ContentType "application/json" -Body '{"email":"testuser@example.com","password":"Test123!","firstName":"Test","lastName":"User"}'

# Login
$login = Invoke-RestMethod -Uri "http://localhost:5178/api/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"testuser@example.com","password":"Test123!"}'
$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }

# Create Order
$orderBody = '{"items":[{"productId":"11111111-1111-1111-1111-111111111111","productName":"Test Product","quantity":2,"price":29.99}]}'
$order = Invoke-RestMethod -Uri "http://localhost:5179/api/orders" -Method POST -Headers $headers -ContentType "application/json" -Body $orderBody
$order | ConvertTo-Json

# Get Order
Invoke-RestMethod -Uri "http://localhost:5179/api/orders/$($order.id)" -Headers $headers | ConvertTo-Json
```

#### Option 3: Through API Gateway

All requests can also be routed through the API Gateway on port 8080:

```bash
# Login via Gateway
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@example.com","password":"Test123!"}'

# Create Order via Gateway
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{"items":[{"productId":"...","productName":"Product","quantity":1,"price":9.99}]}'
```

## 📡 API Endpoints

### Authentication (Identity Service)

| Method | Endpoint             | Description          | Auth |
| ------ | -------------------- | -------------------- | ---- |
| POST   | `/api/auth/register` | Register new user    | No   |
| POST   | `/api/auth/login`    | Login, get tokens    | No   |
| POST   | `/api/auth/refresh`  | Refresh access token | No   |

### Orders (Order Service)

| Method | Endpoint                      | Description           | Auth |
| ------ | ----------------------------- | --------------------- | ---- |
| POST   | `/api/orders`                 | Create new order      | JWT  |
| GET    | `/api/orders/{id}`            | Get order by ID       | JWT  |
| GET    | `/api/orders?status={status}` | List orders by status | JWT  |
| PUT    | `/api/orders/{id}/cancel`     | Cancel order          | JWT  |

## 🔄 Order Lifecycle

```
PENDING → PROCESSING → SHIPPED → DELIVERED
    │
    └──→ CANCELLED (only from PENDING)
```

- Orders start in `PENDING` status
- Only `PENDING` orders can be cancelled
- The `OrderProcessor` background worker advances orders through states

## ⚙️ Configuration

### Connection Strings

Located in `appsettings.json` for each service:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=OrderDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  }
}
```

### JWT Settings

```json
{
  "Jwt": {
    "Key": "your-256-bit-secret-key-here",
    "Issuer": "OrderPlatform",
    "Audience": "OrderPlatform",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

## 🐳 Docker Deployment

### Full Stack Deployment

```bash
docker-compose up -d
```

### View Running Containers

```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

### View Logs

```bash
docker-compose logs -f order-service
docker-compose logs -f identity-service
```

## 🧹 Cleanup

### Stop Services

```bash
# Stop infrastructure
docker-compose -f docker-compose.infrastructure.yml down

# Stop all (including volumes)
docker-compose down -v
```

### Clean Build Artifacts

```bash
dotnet clean
```

## 📊 Database Schema

### IdentityDb

- `Users` - User accounts
- `Roles` - User roles (Customer, Admin)
- `UserRoles` - User-role mappings
- `RefreshTokens` - Token storage for rotation

### OrderDb

- `Orders` - Order headers
- `OrderItems` - Order line items

## 🔍 Troubleshooting

### Port Already in Use

```powershell
# Find process using port
Get-NetTCPConnection -LocalPort 5179 | Select-Object OwningProcess

# Kill process
Stop-Process -Id <PID> -Force
```

### Database Connection Failed

1. Verify SQL Server container is running:

   ```bash
   docker ps | grep sql-server
   ```

2. Test connection:
   ```bash
   docker exec sql-server /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -Q "SELECT 1"
   ```

### Kafka Connection Issues

1. Verify Kafka is running:

   ```bash
   docker ps | grep kafka
   ```

2. Check Kafka logs:
   ```bash
   docker logs kafka
   ```

## 📄 License

This project is licensed under the MIT License.

## 👥 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
