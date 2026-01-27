using Dapper;

namespace IdentityService.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        _logger.LogInformation("Checking if database tables exist...");

        // Check if Users table exists
        var usersTableExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'Users'");

        if (usersTableExists == 0)
        {
            _logger.LogInformation("Creating Users table...");
            await CreateUsersTableAsync(connection);
        }

        var rolesTableExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'Roles'");

        if (rolesTableExists == 0)
        {
            _logger.LogInformation("Creating Roles table...");
            await CreateRolesTableAsync(connection);
        }

        var userRolesTableExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'UserRoles'");

        if (userRolesTableExists == 0)
        {
            _logger.LogInformation("Creating UserRoles table...");
            await CreateUserRolesTableAsync(connection);
        }

        var refreshTokensTableExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'RefreshTokens'");

        if (refreshTokensTableExists == 0)
        {
            _logger.LogInformation("Creating RefreshTokens table...");
            await CreateRefreshTokensTableAsync(connection);
        }

        _logger.LogInformation("Database initialization complete.");
    }

    private async Task CreateUsersTableAsync(System.Data.IDbConnection connection)
    {
        const string sql = @"
            CREATE TABLE Users (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
                UserName NVARCHAR(256) NOT NULL,
                NormalizedUserName NVARCHAR(256) NOT NULL,
                Email NVARCHAR(256) NOT NULL,
                NormalizedEmail NVARCHAR(256) NOT NULL,
                EmailConfirmed BIT NOT NULL DEFAULT 0,
                PasswordHash NVARCHAR(MAX) NULL,
                SecurityStamp NVARCHAR(MAX) NULL,
                ConcurrencyStamp NVARCHAR(MAX) NULL,
                PhoneNumber NVARCHAR(50) NULL,
                PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
                TwoFactorEnabled BIT NOT NULL DEFAULT 0,
                LockoutEnd DATETIMEOFFSET NULL,
                LockoutEnabled BIT NOT NULL DEFAULT 1,
                AccessFailedCount INT NOT NULL DEFAULT 0,
                FirstName NVARCHAR(100) NOT NULL DEFAULT '',
                LastName NVARCHAR(100) NOT NULL DEFAULT '',
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
            );

            CREATE UNIQUE INDEX IX_Users_NormalizedUserName ON Users(NormalizedUserName);
            CREATE UNIQUE INDEX IX_Users_NormalizedEmail ON Users(NormalizedEmail);";

        await connection.ExecuteAsync(sql);
    }

    private async Task CreateRolesTableAsync(System.Data.IDbConnection connection)
    {
        const string sql = @"
            CREATE TABLE Roles (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
                Name NVARCHAR(256) NOT NULL,
                NormalizedName NVARCHAR(256) NOT NULL,
                ConcurrencyStamp NVARCHAR(MAX) NULL
            );

            CREATE UNIQUE INDEX IX_Roles_NormalizedName ON Roles(NormalizedName);";

        await connection.ExecuteAsync(sql);
    }

    private async Task CreateUserRolesTableAsync(System.Data.IDbConnection connection)
    {
        const string sql = @"
            CREATE TABLE UserRoles (
                UserId UNIQUEIDENTIFIER NOT NULL,
                RoleId UNIQUEIDENTIFIER NOT NULL,
                PRIMARY KEY (UserId, RoleId),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
            );";

        await connection.ExecuteAsync(sql);
    }

    private async Task CreateRefreshTokensTableAsync(System.Data.IDbConnection connection)
    {
        const string sql = @"
            CREATE TABLE RefreshTokens (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
                UserId UNIQUEIDENTIFIER NOT NULL,
                Token NVARCHAR(500) NOT NULL,
                ExpiresAt DATETIME2 NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsRevoked BIT NOT NULL DEFAULT 0,
                IsUsed BIT NOT NULL DEFAULT 0,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
            CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);";

        await connection.ExecuteAsync(sql);
    }
}
