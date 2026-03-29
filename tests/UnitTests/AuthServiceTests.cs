using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;
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

        var registerAuth = await service.RegisterAsync(new RegisterRequest(email, initialPassword, "Reset Flow Test"));
        registerAuth.ShowOnboardingWorkbookEmailMessage.Should().BeTrue();

        var resetToken = await service.ForgotPasswordAsync(new ForgotPasswordRequest(email));
        resetToken.Should().NotBeNullOrWhiteSpace();
        CapturingEmailSender.SentMessages.Should().HaveCount(2);
        CapturingEmailSender.SentMessages[0].ToAddress.Should().Be(email);
        CapturingEmailSender.SentMessages[0].Subject.Should().Contain("onboarding sample");
        CapturingEmailSender.SentMessages[0].Attachments.Should().ContainSingle();
        CapturingEmailSender.SentMessages[0].Attachments![0].FileName.Should().Be("sample-onboarding-import-v2.xlsx");
        CapturingEmailSender.SentMessages[1].ToAddress.Should().Be(email);
        CapturingEmailSender.SentMessages[1].PlainTextBody.Should().Contain(Uri.EscapeDataString(resetToken!));

        await service.ResetPasswordAsync(new ResetPasswordRequest(email, resetToken!, updatedPassword));

        var auth = await service.LoginAsync(new LoginRequest(email, updatedPassword));
        auth.Email.Should().Be(email);
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.ShowOnboardingWorkbookEmailMessage.Should().BeTrue();
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
        auth.ShowOnboardingWorkbookEmailMessage.Should().BeTrue();
        CapturingEmailSender.SentMessages.Should().ContainSingle();
        CapturingEmailSender.SentMessages[0].Subject.Should().Contain("onboarding sample");
        CapturingEmailSender.SentMessages[0].Attachments.Should().ContainSingle();

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
        CapturingEmailSender.Reset();
        var workbookPath = Path.Combine(Path.GetTempPath(), $"onboarding-sample-{Guid.NewGuid():N}.xlsx");
        File.WriteAllBytes(workbookPath, [0x01, 0x02, 0x03, 0x04]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(settings)
            {
                ["App:BaseUrl"] = "https://example.test",
                ["Email:OnboardingWorkbookPath"] = workbookPath,
                ["Jwt:Key"] = "ChangeThisInProduction_AtLeast32Chars",
                ["Jwt:Issuer"] = "PersonalFinanceTracker",
                ["Jwt:Audience"] = "PersonalFinanceTrackerClients"
            })
            .Build();

        return new AuthService(dbContext, configuration, new CapturingEmailSender());
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public static List<AppEmailMessage> SentMessages { get; } = [];

        public Task SendAsync(AppEmailMessage message, CancellationToken ct = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public static void Reset() => SentMessages.Clear();
    }
}
