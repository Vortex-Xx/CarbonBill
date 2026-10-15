using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CarbonBill.IntegrationTests;

public sealed class AuthAndRegistrationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly IdentityTenancyDbContext _dbContext;
    private readonly PasswordHasher _passwordHasher;
    private readonly TokenService _tokenService;

    public AuthAndRegistrationIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _tenantContext = new TenantContext();
        var pragma = new SqlitePragmaInterceptor();
        var interceptor = new TenantSaveChangesInterceptor(_tenantContext);

        var options = new DbContextOptionsBuilder<IdentityTenancyDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(pragma, interceptor)
            .Options;

        _dbContext = new IdentityTenancyDbContext(options, _tenantContext);
        _dbContext.Database.EnsureCreated();

        _passwordHasher = new PasswordHasher();
        _tokenService = new TokenService(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "SuperSecretKeyForIntegrationTestingOnly1234567890!",
                ["Jwt:Issuer"] = "CarbonBill.Api",
                ["Jwt:Audience"] = "CarbonBill.Client"
            })
            .Build());
    }

    [Fact]
    public async Task Register_And_Login_With_Email_And_Phone_Succeeds()
    {
        // 1. Arrange & Register
        var org = new Organization
        {
            Name = "Integration Garments Ltd.",
            Slug = "int-garments",
            Sector = "RMG",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        var user = new User
        {
            FullName = "Test Garment Owner",
            Email = "owner@intgarments.local",
            PhoneNumber = "01712345678",
            PasswordHash = _passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var membership = new Membership
        {
            User = user,
            UserId = user.Id,
            Organization = org,
            OrgId = org.Id,
            IsActive = true
        };
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // 2. Act & Assert: Login with email
        var foundByEmail = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Memberships)
            .ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(u => u.IsActive && u.Email == "owner@intgarments.local");

        Assert.NotNull(foundByEmail);
        Assert.True(_passwordHasher.VerifyPassword("Pass1234!", foundByEmail.PasswordHash));
        Assert.False(_passwordHasher.VerifyPassword("WrongPassword!", foundByEmail.PasswordHash));

        // 3. Act & Assert: Login with phone number
        var loginPhone = "+8801712345678";
        var cleanPhone = loginPhone.Replace(" ", "").Replace("-", "");
        var phonePrefixStripped = cleanPhone.StartsWith("+88", StringComparison.Ordinal) ? cleanPhone[3..] : cleanPhone;

        var foundByPhone = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Memberships)
            .ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(u => u.IsActive && (
                u.PhoneNumber == loginPhone ||
                u.PhoneNumber == cleanPhone ||
                u.PhoneNumber == phonePrefixStripped));

        Assert.NotNull(foundByPhone);
        Assert.Equal(foundByEmail.Id, foundByPhone.Id);

        // 4. Act & Assert: Token Generation
        var activeMem = foundByPhone.Memberships.First();
        var tokens = _tokenService.GenerateTokens(foundByPhone, activeMem);
        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
    }

    [Fact]
    public void PasswordReset_Fields_Exist_On_User_Entity()
    {
        var user = new User
        {
            Email = "reset@test.local",
            FullName = "Reset Test",
            PasswordHash = _passwordHasher.HashPassword("Pass1234!"),
            PasswordResetTokenHash = "hash123",
            PasswordResetExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };

        Assert.Equal("hash123", user.PasswordResetTokenHash);
        Assert.NotNull(user.PasswordResetExpiresAtUtc);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
