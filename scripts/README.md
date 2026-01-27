# SQL Server Scripts

This folder contains SQL scripts for setting up and managing the Order Platform databases.

## Scripts Overview

| Script                    | Purpose                              | When to Run         |
| ------------------------- | ------------------------------------ | ------------------- |
| `01-create-databases.sql` | Creates databases and tables         | Initial setup       |
| `02-seed-data.sql`        | Seeds roles (Admin, Customer)        | After EF migrations |
| `03-sample-data.sql`      | Creates sample orders for testing    | Development only    |
| `04-useful-queries.sql`   | Common queries for debugging         | As needed           |
| `05-cleanup.sql`          | Deletes test data or drops databases | Development only    |

## Quick Start

### Option 1: Using Docker (Recommended)

```powershell
# Start SQL Server container
docker-compose -f docker-compose.infrastructure.yml up -d sql-server

# Wait for SQL Server to be ready (about 30 seconds)
Start-Sleep -Seconds 30

# Run the setup script
sqlcmd -S localhost,1433 -U sa -P "YourStrong!Passw0rd" -i scripts/01-create-databases.sql
```

### Option 2: Using SQL Server Management Studio (SSMS)

1. Connect to `localhost,1433` with:
   - Username: `sa`
   - Password: `YourStrong!Passw0rd`

2. Open and execute `01-create-databases.sql`

3. Run EF Core migrations for Identity tables:

   ```powershell
   dotnet ef database update --project src/IdentityService/IdentityService
   ```

4. Execute `02-seed-data.sql` to seed roles

### Option 3: Using Azure Data Studio

Same steps as SSMS, but with Azure Data Studio UI.

## Database Schema

### IdentityDb

```
┌─────────────────────────────────────────┐
│              AspNetUsers                 │  (Created by EF Core)
├─────────────────────────────────────────┤
│ Id (GUID, PK)                           │
│ Email, FirstName, LastName              │
│ PasswordHash, SecurityStamp             │
│ ... (other Identity columns)            │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│            RefreshTokens                 │
├─────────────────────────────────────────┤
│ Id (GUID, PK)                           │
│ UserId (FK → AspNetUsers)               │
│ Token (NVARCHAR)                        │
│ ExpiresAt, CreatedAt, RevokedAt         │
│ ReplacedByToken                         │
└─────────────────────────────────────────┘
```

### OrderDb

```
┌─────────────────────────────────────────┐
│               Orders                     │
├─────────────────────────────────────────┤
│ OrderId (GUID, PK)                      │
│ UserId (GUID)                           │
│ Status (INT)                            │
│ TotalAmount (DECIMAL)                   │
│ CreatedAt, UpdatedAt (DATETIME2)        │
└─────────────────────────────────────────┘
           │
           │ 1:N (CASCADE DELETE)
           ▼
┌─────────────────────────────────────────┐
│             OrderItems                   │
├─────────────────────────────────────────┤
│ OrderItemId (GUID, PK)                  │
│ OrderId (FK → Orders)                   │
│ ProductId (GUID)                        │
│ ProductName (NVARCHAR)                  │
│ Quantity (INT)                          │
│ UnitPrice (DECIMAL)                     │
└─────────────────────────────────────────┘
```

## Order Status Values

| Value | Status     | Description                        |
| ----- | ---------- | ---------------------------------- |
| 0     | Pending    | Order created, awaiting processing |
| 1     | Processing | Order picked up by OrderProcessor  |
| 2     | Shipped    | Order dispatched                   |
| 3     | Delivered  | Order completed                    |
| 4     | Cancelled  | Order cancelled by user            |

## Indexes

### Orders Table

- `IX_Orders_Status` - For filtering by status
- `IX_Orders_UserId` - For "my orders" queries
- `IX_Orders_CreatedAt` - For date range reports

### OrderItems Table

- `IX_OrderItems_OrderId` - For eager loading items with orders

### RefreshTokens Table

- `IX_RefreshTokens_Token` - For token validation
- `IX_RefreshTokens_UserId` - For user token revocation

## Connection Strings

### Local Development (Docker)

```
Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
Server=localhost,1433;Database=OrderDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
```

### Docker Compose (Container Network)

```
Server=sql-server;Database=IdentityDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
Server=sql-server;Database=OrderDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
```
