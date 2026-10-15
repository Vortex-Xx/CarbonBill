using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarbonBill.Modules.IdentityTenancy.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarbonBill.Modules.IdentityTenancy.Services;

public record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    Guid ActiveOrgId);

public interface ITokenService
{
    AuthTokens GenerateTokens(User user, Membership activeMembership);
    string HashRefreshToken(string rawToken);
    ClaimsPrincipal? ValidateToken(string token);
}

public class TokenService(IConfiguration configuration) : ITokenService
{
    private readonly string _jwtKey = configuration["Jwt:Key"] ?? "CarbonBill-SuperSecret-Development-Key-2026-VeryLongKeyNeeded";
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "CarbonBill";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "CarbonBillClients";
    private readonly int _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 15;

    public AuthTokens GenerateTokens(User user, Membership activeMembership)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtKey);
        var expires = DateTime.UtcNow.AddMinutes(_expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new("org_id", activeMembership.OrgId.ToString()),
            new("pref_lang", user.PreferredLanguage)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        return new AuthTokens(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresAtUtc: expires,
            ActiveOrgId: activeMembership.OrgId);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtKey);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
