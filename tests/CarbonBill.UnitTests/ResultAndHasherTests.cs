using CarbonBill.Modules.Calculation;
using CarbonBill.Modules.Calculation.Services;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.SharedKernel.Domain;
using Xunit;

namespace CarbonBill.UnitTests;

public class ResultAndHasherTests
{
    [Fact]
    public void Result_SuccessAndFailure_WorkAsExpected()
    {
        var success = Result.Success("Sample Value");
        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
        Assert.Equal("Sample Value", success.Value);

        var failure = Result.Failure<string>("Something went wrong");
        Assert.True(failure.IsFailure);
        Assert.False(failure.IsSuccess);
        Assert.Equal("Something went wrong", failure.Error);
        Assert.Throws<InvalidOperationException>(() => _ = failure.Value);
    }

    [Fact]
    public void PasswordHasher_HashesAndVerifiesCorrectly()
    {
        var hasher = new PasswordHasher();
        const string password = "StrongPassword2026!";

        var hash = hasher.HashPassword(password);
        Assert.NotNull(hash);
        Assert.NotEqual(password, hash);

        Assert.True(hasher.VerifyPassword(password, hash));
        Assert.False(hasher.VerifyPassword("WrongPassword", hash));
    }

    [Fact]
    public void CalculationEngine_CalculatesAccurately()
    {
        var engine = new CalculationEngine();
        
        // 4520.5 kWh * 0.536 kg CO2e/kWh (BD Grid factor)
        var kwh = 4520.5m;
        var gridFactor = 0.536m;
        var kgCo2e = engine.CalculateKgCo2e(kwh, gridFactor);

        Assert.Equal(2422.9880m, kgCo2e);
    }
}
