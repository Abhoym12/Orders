using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Data;
using IdentityService.Dtos;
using IdentityService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Services;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(RefreshTokenRequest request);
    Task RevokeTokenAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private const int AccessTokenExpirationMinutes = 15;
    private const int RefreshTokenExpirationDays = 7;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: User with email {Email} already exists", request.Email);
            return null;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            NormalizedUserName = normalizedEmail,
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.CreateAsync(user);

        // Assign default Customer role
        var customerRole = await _roleRepository.GetByNameAsync("CUSTOMER");
        if (customerRole != null)
        {
            await _userRepository.AddToRoleAsync(user.Id, customerRole.Id);
        }

        _logger.LogInformation("User {Email} registered successfully", request.Email);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);

        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Email} not found", request.Email);
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed: Invalid password for user {Email}", request.Email);
            return null;
        }

        _logger.LogInformation("User {Email} logged in successfully", request.Email);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found");
            return null;
        }

        if (storedToken.IsUsed || storedToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token has already been used or revoked");
            return null;
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token has expired");
            return null;
        }

        // Mark old token as used (single-use rotation)
        storedToken.IsUsed = true;
        await _refreshTokenRepository.UpdateAsync(storedToken);

        var user = await _userRepository.GetByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogWarning("User not found for refresh token");
            return null;
        }

        _logger.LogInformation("Refresh token rotated for user {UserId}", storedToken.UserId);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

        if (storedToken != null)
        {
            storedToken.IsRevoked = true;
            await _refreshTokenRepository.UpdateAsync(storedToken);
            _logger.LogInformation("Refresh token revoked");
        }
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userRepository.GetRolesAsync(user.Id);
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);
        var expiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes);

        return new AuthResponse(
            accessToken,
            refreshToken,
            expiresAt,
            new UserInfo(user.Id, user.Email!, user.FirstName, user.LastName, roles));
    }

    private string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var refreshToken = Convert.ToBase64String(randomBytes);

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(token);

        return refreshToken;
    }
}
