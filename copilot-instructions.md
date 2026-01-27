---

# 🧠 GitHub Copilot Workspace Instructions

**Project:** E-Commerce Order Processing Platform
**Stack:** .NET 8, Microservices, Clean Architecture, Event-Driven
**Target Scale:** 50,000+ concurrent users

---

## 1. Global Rules for Copilot

You are a **senior software architect and backend engineer**.

You must:

- Follow **Clean Architecture**
- Use **DDD aggregates**
- Use **CQRS + MediatR**
- Use **async/await everywhere**
- Never place business logic in controllers
- Never allow synchronous service-to-service calls
- Use **Kafka** for all integration
- Design for **horizontal scalability**

All code must be **production-grade**, not demo code.

---

## 2. Solution Layout

Create the following solution structure:

```
/order-platform
│
├── src
│   ├── ApiGateway
│   ├── IdentityService
│   ├── OrderService
│   ├── OrderProcessor
│   └── Shared
│        ├── Contracts
│        └── Messaging
│
├── tests
│   ├── OrderService.Domain.Tests
│   ├── OrderService.Application.Tests
│   └── OrderService.Api.Tests
│
└── docker-compose.yml
```

Each service must be **independently runnable**.

---

## 3. Architecture Contract

You must enforce:

```
API → Application → Domain → Infrastructure
```

Domain must not depend on anything.

Application can depend on Domain.

Infrastructure depends on everything.

---

## 4. Order Domain Rules

Order lifecycle:

```
PENDING → PROCESSING → SHIPPED → DELIVERED
```

Only PENDING orders can be cancelled.

This rule must be enforced in **Domain layer only**.

---

## 5. Database Schema

Use SQL Server.

```sql
Orders
- OrderId (GUID PK)
- UserId
- Status
- CreatedAt
- UpdatedAt
- TotalAmount

OrderItems
- Id
- OrderId (FK)
- ProductId
- Quantity
- Price
```

Index:

```
IX_Orders_Status
IX_Orders_UserId
```

---

## 6. API Endpoints

| Method | Endpoint            |
| ------ | ------------------- |
| POST   | /orders             |
| GET    | /orders/{id}        |
| GET    | /orders?status=     |
| PUT    | /orders/{id}/cancel |

All endpoints require **JWT**.

---

## 7. Authentication

Use **Identity Service** with:

- ASP.NET Identity
- JWT
- Roles: Customer, Admin

API Gateway must validate JWT.

---

## 8. Event System

Use Kafka.

Publish:

```
OrderCreated
OrderStatusChanged
OrderCancelled
```

No API may call another API directly.

---

## 9. Background Processing

OrderProcessor must:

```
Every 5 minutes:
    Find Orders where Status = PENDING
    Update to PROCESSING
    Publish OrderStatusChanged
```

Must run in parallel.

---

## 10. Performance Rules

Use:

- Redis for order caching
- Async EF Core
- Parallel.ForEachAsync for workers

---

## 11. CQRS + MediatR

Implement commands:

```
CreateOrder
CancelOrder
```

Queries:

```
GetOrderById
ListOrders
```

Handlers must publish events.

---

## 12. Unit Testing Rules

Use **xUnit**.

You must write:

### Domain tests

- Cancel only allowed in Pending
- Status transition rules

### Application tests

- CancelOrder updates DB
- Events published

### API tests

- Auth required
- Status codes

Use InMemory EF Core.

---

## 13. Docker Compose

Must include:

- SQL Server
- RabbitMQ
- Redis
- OrderService
- OrderProcessor
- IdentityService

All services must communicate via container names.

---

## 14. Non-Negotiable Rules

❌ No business logic in controllers
❌ No direct DB access from API
❌ No service-to-service REST calls
❌ No blocking calls

✔ Domain Driven Design
✔ Event driven
✔ Cloud ready

---

## 15. Final Objective

You must generate a **fully working microservice system** that can handle **50,000+ users** with:

- Clean Architecture
- Background processing
- Event-driven communication
- Secure JWT auth
- Full test coverage

---
