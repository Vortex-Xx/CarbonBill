using System.Security.Claims;
using System.Security.Cryptography;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.IdentityTenancy.Endpoints;

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string OrganizationName,
    string? PhoneNumber = null,
    string? Sector = "RMG",
    string? FacilityType = null);

public record LoginRequest(
    string? Identifier,
    string Password,
    string? Email = null,
    string? PhoneNumber = null,
    Guid? PreferredOrgId = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    Guid ActiveOrgId,
    string ActiveOrgName,
    IReadOnlyList<UserMembershipDto> Memberships);

public record UserMembershipDto(Guid OrgId, string OrgName);

public record RefreshTokenRequest(string? RefreshToken = null);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record CreateInviteRequest(string? InvitedEmail = null, int ValidityDays = 7, int MaxUses = 1);

public record CreateInviteResponse(string Token, DateTime ExpiresAtUtc, string InviteUrl);

public record AcceptInviteRequest(
    string Token,
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber = null);

public record SwitchOrgRequest(Guid TargetOrgId);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Auth");

        // 1. Sign-Up / Register: Creates User and Organization together
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            IdentityTenancyDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.OrganizationName))
            {
                return Results.Problem(
                    detail: "FullName, Email, Password, and OrganizationName are required.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var existingUser = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (existingUser != null)
            {
                return Results.Problem(
                    detail: "A user with this email address already exists.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "User Already Exists");
            }

            var slug = request.OrganizationName.Trim().ToLowerInvariant()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "");
            if (string.IsNullOrWhiteSpace(slug)) slug = "org-" + Guid.NewGuid().ToString("N")[..8];

            // Ensure unique slug
            var existingSlug = await dbContext.Organizations
                .IgnoreQueryFilters()
                .AnyAsync(o => o.Slug == slug, ct);
            if (existingSlug)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
            }

            var sector = !string.IsNullOrWhiteSpace(request.Sector) ? request.Sector.Trim() : (!string.IsNullOrWhiteSpace(request.FacilityType) ? request.FacilityType.Trim() : "RMG");

            var organization = new Organization
            {
                Name = request.OrganizationName.Trim(),
                Slug = slug,
                Sector = sector,
                IsActive = true
            };
            dbContext.Organizations.Add(organization);

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = email,
                PhoneNumber = request.PhoneNumber?.Trim(),
                PasswordHash = passwordHasher.HashPassword(request.Password),
                PreferredLanguage = "en",
                IsActive = true
            };
            dbContext.Users.Add(user);

            var membership = new Membership
            {
                User = user,
                UserId = user.Id,
                Organization = organization,
                OrgId = organization.Id,
                IsActive = true
            };
            dbContext.Memberships.Add(membership);

            await dbContext.SaveChangesAsync(ct);

            var tokens = tokenService.GenerateTokens(user, membership);
            var hashedRefreshToken = tokenService.HashRefreshToken(tokens.RefreshToken);

            dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashedRefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });
            await dbContext.SaveChangesAsync(ct);

            httpResponse.Cookies.Append("carbonbill_refresh", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            var response = new LoginResponse(
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                ExpiresAtUtc: tokens.ExpiresAtUtc,
                UserId: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                ActiveOrgId: organization.Id,
                ActiveOrgName: organization.Name,
                Memberships: [new UserMembershipDto(organization.Id, organization.Name)]);

            return Results.Created($"/api/v1/auth/users/{user.Id}", response);
        });

        // 2. Login: Email or phone number + password
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            IdentityTenancyDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            var loginId = (request.Identifier ?? request.Email ?? request.PhoneNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(loginId))
            {
                return Results.Problem(
                    detail: "Email or phone number is required.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error");
            }

            var isEmail = loginId.Contains('@');
            var normalizedEmail = loginId.ToLowerInvariant();
            var cleanPhone = loginId.Replace(" ", "").Replace("-", "");
            var phonePrefixStripped = cleanPhone.StartsWith("+88", StringComparison.Ordinal) ? cleanPhone[3..] : (cleanPhone.StartsWith("88", StringComparison.Ordinal) ? cleanPhone[2..] : cleanPhone);
            var phoneWithPrefix = "+88" + phonePrefixStripped.TrimStart('+');

            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .Include(u => u.Memberships)
                .ThenInclude(m => m.Organization)
                .FirstOrDefaultAsync(u => u.IsActive && (
                    (isEmail && u.Email == normalizedEmail) ||
                    (!isEmail && (
                        u.PhoneNumber == loginId ||
                        u.PhoneNumber == cleanPhone ||
                        u.PhoneNumber == phonePrefixStripped ||
                        u.PhoneNumber == phoneWithPrefix ||
                        u.Email == normalizedEmail))
                ), ct);

            if (user == null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Results.Problem(
                    detail: "Invalid credentials.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication Failed");
            }

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
            var hashedRefreshToken = tokenService.HashRefreshToken(tokens.RefreshToken);

            dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashedRefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });
            await dbContext.SaveChangesAsync(ct);

            httpResponse.Cookies.Append("carbonbill_refresh", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            var membershipsDto = user.Memberships
                .Where(m => m.IsActive)
                .Select(m => new UserMembershipDto(m.OrgId, m.Organization?.Name ?? "Unknown"))
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
                Memberships: membershipsDto);

            return Results.Ok(response);
        });

        // 3. Refresh: Short-lived JWT + refresh cookie
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
                .IgnoreQueryFilters()
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
                .Select(m => new UserMembershipDto(m.OrgId, m.Organization?.Name ?? "Unknown"))
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
                Memberships: membershipsDto);

            return Results.Ok(response);
        });

        // 4. Logout: Revoke refresh token & delete cookie
        group.MapPost("/logout", async (
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            IdentityTenancyDbContext dbContext,
            ITokenService tokenService,
            CancellationToken ct) =>
        {
            var rawRefreshToken = httpRequest.Cookies["carbonbill_refresh"];
            if (!string.IsNullOrEmpty(rawRefreshToken))
            {
                var hashedToken = tokenService.HashRefreshToken(rawRefreshToken);
                var tokenEntity = await dbContext.RefreshTokens
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken && rt.RevokedAtUtc == null, ct);

                if (tokenEntity != null)
                {
                    tokenEntity.RevokedAtUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            httpResponse.Cookies.Delete("carbonbill_refresh");
            return Results.Ok(new { message = "Logged out successfully." });
        });

        // 5. Password Reset Request (forgot-password)
        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            IdentityTenancyDbContext dbContext,
            IPasswordHasher passwordHasher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email is required." });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

            if (user != null)
            {
                var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                user.PasswordResetTokenHash = passwordHasher.HashPassword(rawToken);
                user.PasswordResetExpiresAtUtc = DateTime.UtcNow.AddHours(2);
                await dbContext.SaveChangesAsync(ct);

                // Log token for development/audit; can be dispatched via email provider
                loggerFactory.CreateLogger("AuthEndpoints")
                    .LogInformation("[Auth] Password reset token generated for {Email}: {Token}", user.Email, rawToken);
            }

            // Always return 200 OK to prevent email enumeration
            return Results.Ok(new
            {
                message = "If an account exists with that email, a password reset link has been dispatched."
            });
        });

        // 6. Password Reset Submission (reset-password)
        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            IdentityTenancyDbContext dbContext,
            IPasswordHasher passwordHasher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return Results.Problem(
                    detail: "Email, Token, and NewPassword are required.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

            if (user == null || string.IsNullOrEmpty(user.PasswordResetTokenHash) ||
                user.PasswordResetExpiresAtUtc == null || user.PasswordResetExpiresAtUtc < DateTime.UtcNow)
            {
                return Results.Problem(
                    detail: "Invalid or expired password reset token.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Reset Token");
            }

            if (!passwordHasher.VerifyPassword(request.Token, user.PasswordResetTokenHash))
            {
                return Results.Problem(
                    detail: "Invalid password reset token.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Reset Token");
            }

            user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            user.PasswordResetTokenHash = null;
            user.PasswordResetExpiresAtUtc = null;
            await dbContext.SaveChangesAsync(ct);

            loggerFactory.CreateLogger("AuthEndpoints")
                .LogInformation("[Auth] Password successfully reset for user {Email}", user.Email);
            return Results.Ok(new { message = "Password reset successfully. You may now log in with your new password." });
        });

        // 7. Invite link generation: Teammates get identical full access (no role choice)
        group.MapPost("/invite", async (
            [FromBody] CreateInviteRequest request,
            IInvitationService invitationService,
            ITenantContext tenantContext,
            HttpRequest httpRequest,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue || !tenantContext.CurrentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await invitationService.CreateInvitationAsync(new CreateInvitationRequest(
                OrgId: tenantContext.CurrentOrgId.Value,
                CreatedByUserId: tenantContext.CurrentUserId.Value,
                InvitedEmail: request.InvitedEmail,
                ValidityPeriod: TimeSpan.FromDays(request.ValidityDays > 0 ? request.ValidityDays : 7),
                MaxUses: request.MaxUses > 0 ? request.MaxUses : 1), ct);

            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var inv = result.Value;
            var host = httpRequest.Host.Value;
            var inviteUrl = $"{httpRequest.Scheme}://{host}/join?token={inv.Token}";

            return Results.Ok(new CreateInviteResponse(
                Token: inv.Token,
                ExpiresAtUtc: inv.ExpiresAtUtc,
                InviteUrl: inviteUrl));
        });

        // 8. Validate Invite Link
        group.MapGet("/invite/{token}", async (
            string token,
            IdentityTenancyDbContext dbContext,
            CancellationToken ct) =>
        {
            var inv = await dbContext.Invitations
                .IgnoreQueryFilters()
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i => i.Token == token, ct);

            if (inv == null || !inv.IsValid())
            {
                return Results.NotFound(new { error = "Invitation token is invalid, expired, or fully used." });
            }

            return Results.Ok(new
            {
                token = inv.Token,
                orgId = inv.OrgId,
                orgName = inv.Organization?.Name ?? "Unknown Organization",
                invitedEmail = inv.InvitedEmail,
                expiresAtUtc = inv.ExpiresAtUtc,
                isValid = true
            });
        });

        // 9. Accept Invite: User registers or joins org with identical full access
        group.MapPost("/accept-invite", async (
            [FromBody] AcceptInviteRequest request,
            IInvitationService invitationService,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            var joinResult = await invitationService.JoinWithInvitationAsync(new JoinWithInvitationRequest(
                Token: request.Token,
                FullName: request.FullName,
                Email: request.Email,
                Password: request.Password,
                PhoneNumber: request.PhoneNumber), ct);

            if (joinResult.IsFailure)
            {
                return Results.Problem(
                    detail: joinResult.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Join Invitation Failed");
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

            var response = new LoginResponse(
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                ExpiresAtUtc: tokens.ExpiresAtUtc,
                UserId: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                ActiveOrgId: membership.OrgId,
                ActiveOrgName: membership.Organization?.Name ?? "",
                Memberships: [new UserMembershipDto(membership.OrgId, membership.Organization?.Name ?? "")]);

            return Results.Ok(response);
        });

        // 10. Me endpoint: Get current user info & active organization
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

            var activeOrg = user.Memberships.FirstOrDefault(m => m.OrgId == tenantContext.CurrentOrgId)?.Organization;

            return Results.Ok(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.PreferredLanguage,
                CurrentOrgId = tenantContext.CurrentOrgId,
                CurrentOrgName = activeOrg?.Name ?? "",
                Memberships = user.Memberships.Where(m => m.IsActive).Select(m => new UserMembershipDto(
                    m.OrgId,
                    m.Organization?.Name ?? "Unknown"))
            });
        });

        // 11. Switch Organization (for users belonging to multiple organizations)
        group.MapPost("/switch-org", async (
            [FromBody] SwitchOrgRequest request,
            ITenantContext tenantContext,
            IdentityTenancyDbContext dbContext,
            ITokenService tokenService,
            HttpResponse httpResponse,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var user = await dbContext.Users
                .Include(u => u.Memberships)
                .ThenInclude(m => m.Organization)
                .FirstOrDefaultAsync(u => u.Id == tenantContext.CurrentUserId.Value && u.IsActive, ct);

            if (user == null)
            {
                return Results.Unauthorized();
            }

            var targetMembership = user.Memberships.FirstOrDefault(m => m.OrgId == request.TargetOrgId && m.IsActive);
            if (targetMembership == null)
            {
                return Results.Problem(
                    detail: "User does not have an active membership in the requested organization.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Switch Organization Denied");
            }

            var tokens = tokenService.GenerateTokens(user, targetMembership);
            var hashedRefreshToken = tokenService.HashRefreshToken(tokens.RefreshToken);

            dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashedRefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });
            await dbContext.SaveChangesAsync(ct);

            httpResponse.Cookies.Append("carbonbill_refresh", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            var membershipsDto = user.Memberships
                .Where(m => m.IsActive)
                .Select(m => new UserMembershipDto(m.OrgId, m.Organization?.Name ?? "Unknown"))
                .ToList();

            var response = new LoginResponse(
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                ExpiresAtUtc: tokens.ExpiresAtUtc,
                UserId: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                ActiveOrgId: targetMembership.OrgId,
                ActiveOrgName: targetMembership.Organization?.Name ?? "",
                Memberships: membershipsDto);

            return Results.Ok(response);
        });

        return endpoints;
    }
}
