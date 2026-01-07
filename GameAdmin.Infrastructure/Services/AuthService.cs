using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using GameAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService(
    AppDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Login attempt for username: {Username}", request.Username);

        // 1. 查找用户
        var user = await dbContext.AdminUsers
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User not found: {Username}", request.Username);
            return null;
        }

        // 检查用户是否被禁用
        if (!user.IsActive)
        {
            logger.LogWarning("User is disabled: {Username}", request.Username);
            return null;
        }

        // 2. 校验密码
        bool isPasswordValid;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BCrypt verification failed for user: {Username}", request.Username);
            return null;
        }

        if (!isPasswordValid)
        {
            logger.LogWarning("Invalid password for user: {Username}", request.Username);
            return null;
        }

        logger.LogInformation("Login successful for user: {Username}", request.Username);

        // 3. 更新最后登录时间
        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // 4. 生成 JWT Token
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var roleNames = user.Roles.Select(r => r.RoleName).ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // 添加角色声明
        foreach (var role in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(
            AccessToken: accessToken,
            ExpiresAt: expiresAt,
            Username: user.Username,
            Roles: roleNames
        );
    }

    /// <inheritdoc />
    public async Task<bool> ValidateSecondaryPasswordAsync(Guid userId, string password)
    {
        // 二次验证使用用户的登录密码
        // 在生产环境中，可以考虑使用单独的操作密码
        var user = await dbContext.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Secondary auth failed: User not found or inactive - {UserId}", userId);
            return false;
        }

        try
        {
            var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            logger.LogInformation("Secondary auth result for {UserId}: {Result}", userId, isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Secondary auth BCrypt verification failed for user: {UserId}", userId);
            return false;
        }
    }
}
