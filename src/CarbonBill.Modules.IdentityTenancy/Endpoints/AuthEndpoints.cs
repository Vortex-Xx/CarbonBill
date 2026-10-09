using System.Security.Claims;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.IdentityTenancy.Endpoints;

public record LoginRequest(string Email, string Password, Guid? PreferredOrgId = null);
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    Guid ActiveOrgId,
    string ActiveOrgName,
    string ActiveRole,
    IReadOnlyList<UserMembershipDto> Memberships);

public record UserMembershipDto(Guid OrgId, string OrgName, string Role);
public record RefreshTokenRequest(string? RefreshToken = null);
public record CreateInviteApiRequest(Guid OrgId, string Role, string Pin, int ValidityDays = 7, int MaxUses = 1);
public record CreateInviteApiResponse(string Token, string Role, DateTime ExpiresAtUtc, string QrPayload);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            IdentityTenancyDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .Include(u => u.Memberships)
                .ThenInclude(m => m.Organization)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

            if (user == null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Results.Problem(
                    detail: "Invalid email or password.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication Failed");
            }

            // Determine active membership
            var activeMembership = request.PreferredOrgId.HasValue
                ? user.Memberships.FirstOrDefault(m => m.OrgId == request.PreferredOrgId.Value && m.IsActive)
                : user.Memberships.FirstOrDefault(m => m.IsActive);

            if (activeMembership == null)
            {
                return Results.Problem(
                    detail: "User has no active organization membership.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "No Active Membership");
            }

            var tokens = tokenService.GenerateTokens(user, activeMembership);

            // Save refresh token
            var hashedRefreshToken = tokenService.HashRefreshToken(tokens.RefreshToken);
            dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashedRefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });
            await dbContext.SaveChangesAsync(ct);

            // Set refresh cookie
            httpResponse.Cookies.Append("carbonbill_refresh", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            var membershipsDto = user.Memberships
                .Where(m => m.IsActive)
                .Select(m => new UserMembershipDto(m.OrgId, m.Organization?.Name ?? "Unknown", m.Role))
                .ToList();

            var response = new LoginResponse(
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                ExpiresAtUtc: tokens.ExpiresAtUtc,
                UserId: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                ActiveOrgId: activeMembership.OrgId,
                ActiveOrgName: activeMembership.Organization?.Name ?? "",
                ActiveRole: activeMembership.Role,
                Memberships: membershipsDto);

            return Results.Ok(response);
        });

        group.MapPost("/refresh", async (
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? body,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            IdentityTenancyDbContext dbContext,
            ITokenService tokenService,
            CancellationToken ct) =>
        {
            var rawRefreshToken = body?.RefreshToken ?? httpRequest.Cookies["carbonbill_refresh"];
            if (string.IsNullOrEmpty(rawRefreshToken))
            {
                return Results.Problem(
                    detail: "Refresh token is missing.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Missing Token");
            }

            var hashedToken = tokenService.HashRefreshToken(rawRefreshToken);
            var tokenEntity = await dbContext.RefreshTokens
                .Include(rt => rt.User)
                .ThenInclude(u => u.Memberships)
                .ThenInclude(m => m.Organization)
                .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken && rt.RevokedAtUtc == null, ct);

            if (tokenEntity == null || tokenEntity.ExpiresAtUtc < DateTime.UtcNow)
            {
                return Results.Problem(
                    detail: "Invalid or expired refresh token.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid Refresh Token");
            }

            // Revoke old token
            tokenEntity.RevokedAtUtc = DateTime.UtcNow;

            var user = tokenEntity.User;
            var activeMembership = user.Memberships.FirstOrDefault(m => m.IsActive);
            if (activeMembership == null)
            {
                return Results.Problem(
                    detail: "User has no active organization membership.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "No Active Membership");
            }

            var newTokens = tokenService.GenerateTokens(user, activeMembership);
            var newHashed = tokenService.HashRefreshToken(newTokens.RefreshToken);

            dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHashed,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });

            await dbContext.SaveChangesAsync(ct);

            httpResponse.Cookies.Append("carbonbill_refresh", newTokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            var membershipsDto = user.Memberships
                .Where(m => m.IsActive)
                .Select(m => new UserMembershipDto(m.OrgId, m.Organization?.Name ?? "Unknown", m.Role))
                .ToList();

            var response = new LoginResponse(
                AccessToken: newTokens.AccessToken,
                RefreshToken: newTokens.RefreshToken,
                ExpiresAtUtc: newTokens.ExpiresAtUtc,
                UserId: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                ActiveOrgId: activeMembership.OrgId,
                ActiveOrgName: activeMembership.Organization?.Name ?? "",
                ActiveRole: activeMembership.Role,
                Memberships: membershipsDto);

            return Results.Ok(response);
        });

        group.MapPost("/join", async (
            [FromBody] JoinWithInvitationRequest request,
            IInvitationService invitationService,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            var joinResult = await invitationService.JoinWithInvitationAsync(request, ct);
            if (joinResult.IsFailure)
            {
                return Results.Problem(
                    detail: joinResult.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invitation Join Failed");
            }

            var (user, membership) = joinResult.Value;
            var tokens = tokenService.GenerateTokens(user, membership);

            httpResponse.Cookies.Append("carbonbill_refresh", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Results.Ok(new
            {
                tokens.AccessToken,
                tokens.RefreshToken,
                tokens.ExpiresAtUtc,
                UserId = user.Id,
                user.FullName,
                membership.OrgId,
                membership.Role
            });
        });

        group.MapPost("/invite", async (
            [FromBody] CreateInviteApiRequest request,
            IInvitationService invitationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var role = tenantContext.CurrentRole;
            var isAuthorized = tenantContext.IsPlatformAdmin ||
                               role is Roles.Owner or Roles.Compliance;

            if (!isAuthorized)
            {
                return Results.Forbid();
            }

            var result = await invitationService.CreateInvitationAsync(new CreateInvitationRequest(
                OrgId: request.OrgId != Guid.Empty ? request.OrgId : (tenantContext.CurrentOrgId ?? Guid.Empty),
                Role: request.Role,
                Pin: request.Pin,
                ValidityPeriod: TimeSpan.FromDays(request.ValidityDays),
                CreatedByUserId: tenantContext.CurrentUserId ?? Guid.Empty,
                MaxUses: request.MaxUses), ct);

            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var inv = result.Value;
            var qrPayload = $"carbonbill://join?token={inv.Token}";

            return Results.Created($"/api/v1/auth/invitations/{inv.Id}", new CreateInviteApiResponse(
                Token: inv.Token,
                Role: inv.Role,
                ExpiresAtUtc: inv.ExpiresAtUtc,
                QrPayload: qrPayload));
        });

        group.MapGet("/me", async (
            ITenantContext tenantContext,
            IdentityTenancyDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var user = await dbContext.Users
                .Include(u => u.Memberships)
                .ThenInclude(m => m.Organization)
                .FirstOrDefaultAsync(u => u.Id == tenantContext.CurrentUserId.Value, ct);

            if (user == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.PreferredLanguage,
                user.IsPlatformAdmin,
                CurrentOrgId = tenantContext.CurrentOrgId,
                CurrentRole = tenantContext.CurrentRole,
                Memberships = user.Memberships.Select(m => new
                {
                    m.OrgId,
                    OrgName = m.Organization?.Name ?? "Unknown",
                    m.Role,
                    m.IsActive
                })
            });
        });

        return endpoints;
    }
}
