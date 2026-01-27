using Dapper;
using IdentityService.Models;

namespace IdentityService.Data;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<RefreshToken?> GetByTokenWithUserAsync(string token);
    Task CreateAsync(RefreshToken refreshToken);
    Task UpdateAsync(RefreshToken refreshToken);
    Task RevokeAllForUserAsync(Guid userId);
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IUserRepository _userRepository;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory, IUserRepository userRepository)
    {
        _connectionFactory = connectionFactory;
        _userRepository = userRepository;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        const string sql = @"
            SELECT Id, UserId, Token, ExpiresAt, CreatedAt, IsRevoked, IsUsed
            FROM RefreshTokens
            WHERE Token = @Token";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(sql, new { Token = token });
    }

    public async Task<RefreshToken?> GetByTokenWithUserAsync(string token)
    {
        var refreshToken = await GetByTokenAsync(token);
        return refreshToken;
    }

    public async Task CreateAsync(RefreshToken refreshToken)
    {
        const string sql = @"
            INSERT INTO RefreshTokens (Id, UserId, Token, ExpiresAt, CreatedAt, IsRevoked, IsUsed)
            VALUES (@Id, @UserId, @Token, @ExpiresAt, @CreatedAt, @IsRevoked, @IsUsed)";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, refreshToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        const string sql = @"
            UPDATE RefreshTokens SET
                IsRevoked = @IsRevoked,
                IsUsed = @IsUsed
            WHERE Id = @Id";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, refreshToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        const string sql = @"
            UPDATE RefreshTokens SET IsRevoked = 1
            WHERE UserId = @UserId AND IsRevoked = 0 AND IsUsed = 0";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }
}
