-- ============================================================================
-- Order Platform - Database Setup Scripts
-- ============================================================================
-- This script creates both databases required for the Order Platform:
-- 1. IdentityDb - User authentication and refresh tokens
-- 2. OrderDb - Orders and order items
--
-- Prerequisites: SQL Server 2019+ or Azure SQL
-- Run as: sa or a user with CREATE DATABASE permissions
-- ============================================================================

USE master;
GO

-- ============================================================================
-- SECTION 1: CREATE DATABASES
-- ============================================================================

-- Create IdentityDb
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'IdentityDb')
BEGIN
    CREATE DATABASE IdentityDb;
    PRINT 'Created database: IdentityDb';
END
ELSE
BEGIN
    PRINT 'Database IdentityDb already exists';
END
GO

-- Create OrderDb
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'OrderDb')
BEGIN
    CREATE DATABASE OrderDb;
    PRINT 'Created database: OrderDb';
END
ELSE
BEGIN
    PRINT 'Database OrderDb already exists';
END
GO

-- ============================================================================
-- SECTION 2: IDENTITY DATABASE SCHEMA (Dapper-based, no EF Core)
-- ============================================================================

USE IdentityDb;
GO

-- Create Users table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [UserName] NVARCHAR(256) NOT NULL,
        [NormalizedUserName] NVARCHAR(256) NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [NormalizedEmail] NVARCHAR(256) NOT NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0,
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(50) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
        [LockoutEnd] DATETIMEOFFSET NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 1,
        [AccessFailedCount] INT NOT NULL DEFAULT 0,
        [FirstName] NVARCHAR(100) NOT NULL DEFAULT '',
        [LastName] NVARCHAR(100) NOT NULL DEFAULT '',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE UNIQUE INDEX IX_Users_NormalizedUserName ON [dbo].[Users]([NormalizedUserName]);
    CREATE UNIQUE INDEX IX_Users_NormalizedEmail ON [dbo].[Users]([NormalizedEmail]);

    PRINT 'Created table: Users with indexes';
END
GO

-- Create Roles table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [Name] NVARCHAR(256) NOT NULL,
        [NormalizedName] NVARCHAR(256) NOT NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL
    );

    CREATE UNIQUE INDEX IX_Roles_NormalizedName ON [dbo].[Roles]([NormalizedName]);

    PRINT 'Created table: Roles with indexes';
END
GO

-- Create UserRoles table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserRoles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        PRIMARY KEY ([UserId], [RoleId]),
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE CASCADE
    );

    PRINT 'Created table: UserRoles';
END
GO

-- Create RefreshTokens table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RefreshTokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Token] NVARCHAR(500) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsRevoked] BIT NOT NULL DEFAULT 0,
        [IsUsed] BIT NOT NULL DEFAULT 0,
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_RefreshTokens_Token ON [dbo].[RefreshTokens]([Token]);
    CREATE INDEX IX_RefreshTokens_UserId ON [dbo].[RefreshTokens]([UserId]);

    PRINT 'Created table: RefreshTokens with indexes';
END
GO

-- ============================================================================
-- SECTION 3: ORDER DATABASE SCHEMA
-- ============================================================================

USE OrderDb;
GO

-- Create Orders table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [OrderId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Status] INT NOT NULL DEFAULT 0,  -- 0=Pending, 1=Processing, 2=Shipped, 3=Delivered, 4=Cancelled
        [TotalAmount] DECIMAL(18, 2) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Index for filtering orders by status (common query pattern)
    CREATE NONCLUSTERED INDEX [IX_Orders_Status] 
    ON [dbo].[Orders] ([Status])
    INCLUDE ([UserId], [TotalAmount], [CreatedAt]);

    -- Index for filtering orders by user (my orders query)
    CREATE NONCLUSTERED INDEX [IX_Orders_UserId] 
    ON [dbo].[Orders] ([UserId])
    INCLUDE ([Status], [TotalAmount], [CreatedAt]);

    -- Index for date range queries (reporting)
    CREATE NONCLUSTERED INDEX [IX_Orders_CreatedAt] 
    ON [dbo].[Orders] ([CreatedAt])
    INCLUDE ([Status], [UserId]);

    PRINT 'Created table: Orders with indexes';
END
ELSE
BEGIN
    PRINT 'Table Orders already exists';
END
GO

-- Create OrderItems table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderItems] (
        [OrderItemId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [OrderId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(18, 2) NOT NULL,
        
        CONSTRAINT [FK_OrderItems_Orders] 
            FOREIGN KEY ([OrderId]) 
            REFERENCES [dbo].[Orders] ([OrderId]) 
            ON DELETE CASCADE
    );

    -- Index for fetching items by order (eager loading)
    CREATE NONCLUSTERED INDEX [IX_OrderItems_OrderId] 
    ON [dbo].[OrderItems] ([OrderId])
    INCLUDE ([ProductId], [ProductName], [Quantity], [UnitPrice]);

    PRINT 'Created table: OrderItems with indexes';
END
ELSE
BEGIN
    PRINT 'Table OrderItems already exists';
END
GO

-- ============================================================================
-- SECTION 4: SEED DEFAULT ROLES (Dapper-based Identity)
-- ============================================================================

USE IdentityDb;
GO

-- Seed Admin role
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [NormalizedName] = 'ADMIN')
BEGIN
    INSERT INTO [dbo].[Roles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());
    PRINT 'Seeded role: Admin';
END
GO

-- Seed Customer role
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [NormalizedName] = 'CUSTOMER')
BEGIN
    INSERT INTO [dbo].[Roles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Customer', 'CUSTOMER', NEWID());
    PRINT 'Seeded role: Customer';
END
GO

PRINT '';
PRINT '============================================================================';
PRINT 'Database setup complete!';
PRINT '';
PRINT 'Tables created:';
PRINT '  IdentityDb: Users, Roles, UserRoles, RefreshTokens';
PRINT '  OrderDb: Orders, OrderItems';
PRINT '';
PRINT 'Default roles seeded: Admin, Customer';
PRINT '';
PRINT 'The application will initialize databases on startup via DatabaseInitializer.';
PRINT '============================================================================';
GO
