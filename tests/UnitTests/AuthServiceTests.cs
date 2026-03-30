using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task ResetPassword_Should_Accept_UrlEncoded_Token_From_Email_Link()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext, new Dictionary<string, string?>
        {
            ["Auth:ExposeResetTokenForDemo"] = "true",
            ["OAuth:AllowInsecureTestMode"] = "true"
        });

        var email = $"encoded.{Guid.NewGuid():N}@example.com";
        const string initialPassword = "Initial@123";
        const string updatedPassword = "Updated@123";

        await service.RegisterAsync(new RegisterRequest(email, initialPassword, "Encoded Reset"));
        var resetToken = await service.ForgotPasswordAsync(new ForgotPasswordRequest(email));
        resetToken.Should().NotBeNullOrWhiteSpace();

        var encodedToken = Uri.EscapeDataString(resetToken!);
        await service.ResetPasswordAsync(new ResetPasswordRequest(email, encodedToken, updatedPassword));

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
        auth.ShowOnboardingWorkbookEmailMessage.Should().BeTrue();
        CapturingEmailSender.SentMessages.Should().ContainSingle();
        CapturingEmailSender.SentMessages[0].Subject.Should().Contain("onboarding sample");
        CapturingEmailSender.SentMessages[0].Attachments.Should().ContainSingle();

        var user = await dbContext.Users.SingleAsync(x => x.Email == email);
        user.ProfileImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Register_Should_Fallback_To_Link_Email_When_Workbook_Attachment_Is_Missing()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(
            dbContext,
            new Dictionary<string, string?>(),
            workbookPath: Path.Combine(Path.GetTempPath(), $"missing-sample-{Guid.NewGuid():N}.xlsx"));

        var email = $"fallback.{Guid.NewGuid():N}@example.com";
        var auth = await service.RegisterAsync(new RegisterRequest(email, "Fallback@123", "Fallback User"));

        auth.ShowOnboardingWorkbookEmailMessage.Should().BeTrue();
        CapturingEmailSender.SentMessages.Should().ContainSingle();
        CapturingEmailSender.SentMessages[0].Attachments.Should().BeNull();
        CapturingEmailSender.SentMessages[0].PlainTextBody.Should().Contain("Download it here");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuthService CreateAuthService(
        AppDbContext dbContext,
        IDictionary<string, string?> settings,
        string? workbookPath = null)
    {
        CapturingEmailSender.Reset();
        var shouldCreateWorkbook = string.IsNullOrWhiteSpace(workbookPath);
        workbookPath ??= Path.Combine(Path.GetTempPath(), $"onboarding-sample-{Guid.NewGuid():N}.xlsx");
        if (shouldCreateWorkbook && !File.Exists(workbookPath))
        {
            var directory = Path.GetDirectoryName(workbookPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(workbookPath, [0x01, 0x02, 0x03, 0x04]);
        }

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

        return new AuthService(dbContext, configuration, new CapturingEmailSender(), NullLogger<AuthService>.Instance);
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
