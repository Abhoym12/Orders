-- ============================================================================
-- Order Platform - Seed Data Script
-- ============================================================================
-- This script seeds initial data for development/testing:
-- 1. Default roles (Admin, Customer)
-- 2. Sample admin user
-- 3. Sample orders (optional)
--
-- Run AFTER EF Core migrations have created the Identity tables
-- ============================================================================

USE IdentityDb;
GO

-- ============================================================================
-- SECTION 1: SEED ROLES
-- ============================================================================

-- Insert Admin role if not exists
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = 'Admin')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());
    PRINT 'Created role: Admin';
END
GO

-- Insert Customer role if not exists
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = 'Customer')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Customer', 'CUSTOMER', NEWID());
    PRINT 'Created role: Customer';
END
GO

PRINT 'Roles seeded successfully';
PRINT '';
PRINT 'Note: Admin user should be created via the /api/auth/register endpoint';
PRINT 'Then manually assign Admin role using the query below:';
PRINT '';
PRINT '-- Assign Admin role to a user:';
PRINT '-- DECLARE @UserId UNIQUEIDENTIFIER = (SELECT Id FROM AspNetUsers WHERE Email = ''admin@example.com'');';
PRINT '-- DECLARE @RoleId UNIQUEIDENTIFIER = (SELECT Id FROM AspNetRoles WHERE Name = ''Admin'');';
PRINT '-- INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);';
GO
