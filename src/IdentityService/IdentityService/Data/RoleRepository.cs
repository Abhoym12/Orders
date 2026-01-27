using Dapper;
using IdentityService.Models;

namespace IdentityService.Data;

public interface IRoleRepository
{
    Task<ApplicationRole?> GetByIdAsync(Guid id);
    Task<ApplicationRole?> GetByNameAsync(string normalizedName);
    Task CreateAsync(ApplicationRole role);
    Task UpdateAsync(ApplicationRole role);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(string normalizedName);
}

public class RoleRepository : IRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ApplicationRole?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, NormalizedName, ConcurrencyStamp
            FROM Roles
            WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(sql, new { Id = id });
    }

    public async Task<ApplicationRole?> GetByNameAsync(string normalizedName)
    {
        const string sql = @"
            SELECT Id, Name, NormalizedName, ConcurrencyStamp
            FROM Roles
            WHERE NormalizedName = @NormalizedName";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(sql, new { NormalizedName = normalizedName });
    }

    public async Task CreateAsync(ApplicationRole role)
    {
        const string sql = @"
            INSERT INTO Roles (Id, Name, NormalizedName, ConcurrencyStamp)
            VALUES (@Id, @Name, @NormalizedName, @ConcurrencyStamp)";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, role);
    }

    public async Task UpdateAsync(ApplicationRole role)
    {
        const string sql = @"
            UPDATE Roles SET
                Name = @Name,
                NormalizedName = @NormalizedName,
                ConcurrencyStamp = @ConcurrencyStamp
            WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, role);
    }

    public async Task DeleteAsync(Guid id)
    {
        const string sql = "DELETE FROM Roles WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(string normalizedName)
    {
        const string sql = "SELECT COUNT(1) FROM Roles WHERE NormalizedName = @NormalizedName";

        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new { NormalizedName = normalizedName });
        return count > 0;
    }
}
