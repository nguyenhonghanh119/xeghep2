using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

namespace XeGhepApp.Data;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "XeGhep";
    public string Audience { get; set; } = "XeGhep.Web";
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 7;
}

public static class CurrentUser
{
    public static int DriverId(ClaimsPrincipal user)
    {
        if (!int.TryParse(user.FindFirstValue("user_id"), out var driverId))
            throw new UnauthorizedAccessException("Phiên tài xế không hợp lệ.");
        return driverId;
    }
}

public sealed class DriverTokenService
{
    private readonly JwtSettings _settings;

    public DriverTokenService(IConfiguration configuration)
    {
        _settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    }

    public async Task<(string accessToken, string refreshToken, DateTime accessExpiresAt)> IssueAsync(
        int userId, string fullName, string role, string? ipAddress, string? userAgent)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash = Hash(refreshToken);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshDays);
        int sessionId;

        await using (var conn = await Db.OpenAsync())
        await using (var cmd = new MySqlCommand(@"INSERT INTO user_sessions
            (user_id, token, ip_address, user_agent, expires_at, created_at)
            VALUES (@user_id, @token, @ip_address, @user_agent, @expires_at, UTC_TIMESTAMP())", conn))
        {
            cmd.Parameters.AddWithValue("@user_id", userId);
            cmd.Parameters.AddWithValue("@token", refreshHash);
            cmd.Parameters.AddWithValue("@ip_address", (object?)ipAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@user_agent", (object?)(userAgent?.Length > 255 ? userAgent[..255] : userAgent) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@expires_at", refreshExpiresAt);
            await cmd.ExecuteNonQueryAsync();
            sessionId = checked((int)cmd.LastInsertedId);
        }

        var accessExpiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("user_id", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim("session_id", sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims,
            DateTime.UtcNow, accessExpiresAt, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), refreshToken, accessExpiresAt);
    }

    public void WriteCookies(HttpResponse response, bool secure, string accessToken, string refreshToken, DateTime accessExpiresAt)
    {
        response.Cookies.Append("xeghep_access", accessToken, new CookieOptions
        {
            HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/", Expires = accessExpiresAt,
        });
        response.Cookies.Append("xeghep_refresh", refreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/api/auth", Expires = DateTimeOffset.UtcNow.AddDays(_settings.RefreshDays),
        });
        response.Cookies.Append("XSRF-TOKEN", Convert.ToBase64String(Guid.NewGuid().ToByteArray()), new CookieOptions
        {
            HttpOnly = false, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/",
        });
    }

    public async Task<(string accessToken, string refreshToken, DateTime accessExpiresAt)?> RefreshAsync(
        string refreshToken, string? ipAddress, string? userAgent)
    {
        var refreshHash = Hash(refreshToken);
        int userId;
        string fullName;
        string role;

        await using (var conn = await Db.OpenAsync())
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await using (var select = new MySqlCommand(@"SELECT u.user_id, u.full_name, u.role
                FROM user_sessions s JOIN users u ON u.user_id = s.user_id
                WHERE s.token = @token AND s.revoked_at IS NULL AND s.expires_at > UTC_TIMESTAMP()
                  AND u.role = 'driver' AND u.status = 'active' FOR UPDATE", conn, tx))
            {
                select.Parameters.AddWithValue("@token", refreshHash);
                await using var reader = await select.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                userId = reader.GetInt32("user_id");
                fullName = reader.GetString("full_name");
                role = reader.GetString("role");
            }

            await using var revoke = new MySqlCommand(
                "UPDATE user_sessions SET revoked_at = UTC_TIMESTAMP(), last_used_at = UTC_TIMESTAMP() WHERE token = @token", conn, tx);
            revoke.Parameters.AddWithValue("@token", refreshHash);
            await revoke.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }

        return await IssueAsync(userId, fullName, role, ipAddress, userAgent);
    }

    public async Task LogoutAsync(int sessionId)
    {
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE user_sessions SET revoked_at = UTC_TIMESTAMP() WHERE session_id = @session_id AND revoked_at IS NULL", conn);
        cmd.Parameters.AddWithValue("@session_id", sessionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static void DeleteCookies(HttpResponse response)
    {
        response.Cookies.Delete("xeghep_access", new CookieOptions { Path = "/" });
        response.Cookies.Delete("xeghep_refresh", new CookieOptions { Path = "/api/auth" });
        response.Cookies.Delete("XSRF-TOKEN", new CookieOptions { Path = "/" });
    }

    private string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_settings.Key}:{token}")));
}

public static class SharedAuthentication
{
    public static IServiceCollection AddSharedAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key phải được cấu hình và dài tối thiểu 32 ký tự.");
        services.AddSingleton<DriverTokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token))
                            context.Token = context.Request.Cookies["xeghep_access"];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (!int.TryParse(context.Principal?.FindFirstValue("user_id"), out var userId)
                            || !int.TryParse(context.Principal?.FindFirstValue("session_id"), out var sessionId))
                        {
                            context.Fail("Phiên đăng nhập không hợp lệ.");
                            return;
                        }

                        await using var conn = await Db.OpenAsync();
                        await using var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM user_sessions s
                            INNER JOIN users u ON u.user_id = s.user_id
                            WHERE s.session_id = @session_id AND s.user_id = @user_id
                              AND s.revoked_at IS NULL AND s.expires_at > UTC_TIMESTAMP()
                              AND u.status = 'active'", conn);
                        cmd.Parameters.AddWithValue("@session_id", sessionId);
                        cmd.Parameters.AddWithValue("@user_id", userId);
                        if (Convert.ToInt64(await cmd.ExecuteScalarAsync()) != 1)
                            context.Fail("Phiên đăng nhập đã bị thu hồi.");
                    },
                    OnChallenge = context =>
                    {
                        if (!context.Response.HasStarted && !context.Request.Path.StartsWithSegments("/api")
                            && context.Request.Headers.Accept.Any(x => x?.Contains("text/html") == true))
                        {
                            context.HandleResponse();
                            context.Response.Redirect("/Login");
                        }
                        return Task.CompletedTask;
                    },
                };
            });
        services.AddAuthorization(options =>
            options.AddPolicy("DriverOnly", policy => policy.RequireRole("driver")));
        return services;
    }
}
