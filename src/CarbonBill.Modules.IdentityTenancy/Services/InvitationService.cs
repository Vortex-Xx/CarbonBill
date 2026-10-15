using System.Security.Cryptography;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.IdentityTenancy.Services;

public record CreateInvitationRequest(
    Guid OrgId,
    Guid CreatedByUserId,
    string? InvitedEmail = null,
    TimeSpan? ValidityPeriod = null,
    int MaxUses = 1);

public record JoinWithInvitationRequest(
    string Token,
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber = null);

public interface IInvitationService
{
    Task<Result<Invitation>> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default);
    Task<Result<(User User, Membership Membership)>> JoinWithInvitationAsync(JoinWithInvitationRequest request, CancellationToken cancellationToken = default);
}

public class InvitationService(
    IdentityTenancyDbContext dbContext,
    IPasswordHasher passwordHasher) : IInvitationService
{
    public async Task<Result<Invitation>> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var org = await dbContext.Organizations.FindAsync([request.OrgId], cancellationToken);
        if (org == null)
        {
            return Result.Failure<Invitation>($"Organization with id {request.OrgId} was not found.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var validity = request.ValidityPeriod ?? TimeSpan.FromDays(7);

        var invitation = new Invitation
        {
            OrgId = request.OrgId,
            Token = token,
            InvitedEmail = request.InvitedEmail?.Trim().ToLowerInvariant(),
            ExpiresAtUtc = DateTime.UtcNow.Add(validity),
            CreatedByUserId = request.CreatedByUserId,
            MaxUses = request.MaxUses,
            UseCount = 0
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(invitation);
    }

    public async Task<Result<(User User, Membership Membership)>> JoinWithInvitationAsync(
        JoinWithInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.Invitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken);

        if (invitation == null)
        {
            return Result.Failure<(User, Membership)>("Invalid invitation token.");
        }

        if (!invitation.IsValid())
        {
            return Result.Failure<(User, Membership)>("Invitation is expired or has reached its maximum uses.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(invitation.InvitedEmail) &&
            !string.Equals(invitation.InvitedEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<(User, Membership)>($"This invitation was sent specifically to {invitation.InvitedEmail}.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null)
        {
            user = new User
            {
                Email = email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = passwordHasher.HashPassword(request.Password),
                PreferredLanguage = "en"
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Verify existing user password
            if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Result.Failure<(User, Membership)>("User with this email already exists. Incorrect password provided to join organization.");
            }
        }

        // Check if membership already exists
        var existingMembership = await dbContext.Memberships
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrgId == invitation.OrgId, cancellationToken);

        Membership membership;
        if (existingMembership != null)
        {
            membership = existingMembership;
            membership.IsActive = true;
        }
        else
        {
            membership = new Membership
            {
                UserId = user.Id,
                OrgId = invitation.OrgId,
                IsActive = true
            };
            dbContext.Memberships.Add(membership);
        }

        invitation.UseCount++;
        invitation.UsedByUserId = user.Id;
        invitation.UsedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success((user, membership));
    }
}
