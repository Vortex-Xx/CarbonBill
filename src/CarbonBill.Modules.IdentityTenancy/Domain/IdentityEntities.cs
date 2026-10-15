using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.IdentityTenancy.Domain;

public static class Roles
{
    public const string FloorStaff = "FloorStaff";
    public const string Accountant = "Accountant";
    public const string Compliance = "Compliance";
    public const string Owner = "Owner";
    public const string Consultant = "Consultant";
    public const string PlatformAdmin = "PlatformAdmin";

    public static readonly IReadOnlyList<string> All =
    [
        FloorStaff,
        Accountant,
        Compliance,
        Owner,
        Consultant,
        PlatformAdmin
    ];

    public static bool IsValid(string role) => All.Contains(role);
}

public class Organization : AggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sector { get; set; } = "RMG"; // Default sector as per doc
    public bool IsActive { get; set; } = true;

    public List<Membership> Memberships { get; set; } = [];
}

public class User : AggregateRoot
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "bn"; // Bangla-first
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }

    public string? PinLockHash { get; set; }
    public bool IsLocked { get; set; }

    public List<Membership> Memberships { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public class Membership : BaseEntity, ITenantScopedEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid OrgId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Role { get; set; } = Roles.FloorStaff;
    public bool IsActive { get; set; } = true;
}

public class Invitation : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Role { get; set; } = Roles.FloorStaff;
    public string Token { get; set; } = string.Empty; // QR token
    public string PinHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsRevoked { get; set; }
    public int MaxUses { get; set; } = 1;
    public int UseCount { get; set; }
    public Guid? UsedByUserId { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public bool IsValid() => !IsRevoked && DateTime.UtcNow <= ExpiresAtUtc && UseCount < MaxUses;
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow <= ExpiresAtUtc;
}
