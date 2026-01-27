using Dapper;
using IdentityService.Models;

namespace IdentityService.Data;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(Guid id);
    Task<ApplicationUser?> GetByEmailAsync(string normalizedEmail);
    Task<ApplicationUser?> GetByUserNameAsync(string normalizedUserName);
    Task CreateAsync(ApplicationUser user);
    Task UpdateAsync(ApplicationUser user);
    Task DeleteAsync(Guid id);
    Task<IList<string>> GetRolesAsync(Guid userId);
    Task AddToRoleAsync(Guid userId, Guid roleId);
    Task RemoveFromRoleAsync(Guid userId, Guid roleId);
    Task<bool> IsInRoleAsync(Guid userId, string normalizedRoleName);
}

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, 
                   LockoutEnd, LockoutEnabled, AccessFailedCount,
                   FirstName, LastName, CreatedAt
            FROM Users
            WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { Id = id });
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string normalizedEmail)
    {
        const string sql = @"
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, 
                   LockoutEnd, LockoutEnabled, AccessFailedCount,
                   FirstName, LastName, CreatedAt
            FROM Users
            WHERE NormalizedEmail = @NormalizedEmail";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { NormalizedEmail = normalizedEmail });
    }

    public async Task<ApplicationUser?> GetByUserNameAsync(string normalizedUserName)
    {
        const string sql = @"
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, 
                   LockoutEnd, LockoutEnabled, AccessFailedCount,
                   FirstName, LastName, CreatedAt
            FROM Users
            WHERE NormalizedUserName = @NormalizedUserName";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { NormalizedUserName = normalizedUserName });
    }

    public async Task CreateAsync(ApplicationUser user)
    {
        const string sql = @"
            INSERT INTO Users (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                              EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                              PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                              LockoutEnd, LockoutEnabled, AccessFailedCount,
                              FirstName, LastName, CreatedAt)
            VALUES (@Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
                    @EmailConfirmed, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
                    @PhoneNumber, @PhoneNumberConfirmed, @TwoFactorEnabled,
                    @LockoutEnd, @LockoutEnabled, @AccessFailedCount,
                    @FirstName, @LastName, @CreatedAt)";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, user);
    }

    public async Task UpdateAsync(ApplicationUser user)
    {
        const string sql = @"
            UPDATE Users SET
                UserName = @UserName,
                NormalizedUserName = @NormalizedUserName,
                Email = @Email,
                NormalizedEmail = @NormalizedEmail,
                EmailConfirmed = @EmailConfirmed,
                PasswordHash = @PasswordHash,
                SecurityStamp = @SecurityStamp,
                ConcurrencyStamp = @ConcurrencyStamp,
                PhoneNumber = @PhoneNumber,
                PhoneNumberConfirmed = @PhoneNumberConfirmed,
                TwoFactorEnabled = @TwoFactorEnabled,
                LockoutEnd = @LockoutEnd,
                LockoutEnabled = @LockoutEnabled,
                AccessFailedCount = @AccessFailedCount,
                FirstName = @FirstName,
                LastName = @LastName
            WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, user);
    }

    public async Task DeleteAsync(Guid id)
    {
        const string sql = "DELETE FROM Users WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IList<string>> GetRolesAsync(Guid userId)
    {
        const string sql = @"
            SELECT r.Name
            FROM Roles r
            INNER JOIN UserRoles ur ON r.Id = ur.RoleId
            WHERE ur.UserId = @UserId";

        using var connection = _connectionFactory.CreateConnection();
        var roles = await connection.QueryAsync<string>(sql, new { UserId = userId });
        return roles.ToList();
    }

    public async Task AddToRoleAsync(Guid userId, Guid roleId)
    {
        const string sql = @"
            INSERT INTO UserRoles (UserId, RoleId)
            VALUES (@UserId, @RoleId)";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId });
    }

    public async Task RemoveFromRoleAsync(Guid userId, Guid roleId)
    {
        const string sql = "DELETE FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId });
    }

    public async Task<bool> IsInRoleAsync(Guid userId, string normalizedRoleName)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM UserRoles ur
            INNER JOIN Roles r ON ur.RoleId = r.Id
            WHERE ur.UserId = @UserId AND r.NormalizedName = @NormalizedRoleName";

        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, NormalizedRoleName = normalizedRoleName });
        return count > 0;
    }
}
