using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class AuthServiceTests
{
    [Fact]
    public async Task ForgotPassword_Should_Expose_Reset_Token_In_Demo_Mode_And_Allow_Reset_Login()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext, new Dictionary<string, string?>
        {
            ["Auth:ExposeResetTokenForDemo"] = "true",
            ["OAuth:AllowInsecureTestMode"] = "true"
        });

        var email = $"reset.{Guid.NewGuid():N}@example.com";
        const string initialPassword = "Initial@123";
        const string updatedPassword = "Updated@123";

        await service.RegisterAsync(new RegisterRequest(email, initialPassword, "Reset Flow Test"));

        var resetToken = await service.ForgotPasswordAsync(new ForgotPasswordRequest(email));
        resetToken.Should().NotBeNullOrWhiteSpace();

        await service.ResetPasswordAsync(new ResetPasswordRequest(email, resetToken!, updatedPassword));

        var auth = await service.LoginAsync(new LoginRequest(email, updatedPassword));
        auth.Email.Should().Be(email);
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OAuth_Should_Login_In_Insecure_Test_Mode_Without_IdToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext, new Dictionary<string, string?>
        {
            ["OAuth:AllowInsecureTestMode"] = "true"
        });

        var email = $"oauth.{Guid.NewGuid():N}@example.com";
        var auth = await service.OAuthLoginAsync(new OAuthLoginRequest(
            "google",
            null,
            null,
            email,
            "OAuth Test User"));

        auth.Email.Should().Be(email);
        auth.DisplayName.Should().Be("OAuth Test User");
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var user = await dbContext.Users.SingleAsync(x => x.Email == email);
        user.ProfileImageUrl.Should().BeNull();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuthService CreateAuthService(AppDbContext dbContext, IDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(settings)
            {
                ["Jwt:Key"] = "ChangeThisInProduction_AtLeast32Chars",
                ["Jwt:Issuer"] = "PersonalFinanceTracker",
                ["Jwt:Audience"] = "PersonalFinanceTrackerClients"
            })
            .Build();

        return new AuthService(dbContext, configuration);
    }
}
