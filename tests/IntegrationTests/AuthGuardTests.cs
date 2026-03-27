using System.Net;
using FluentAssertions;

namespace IntegrationTests;

public class AuthGuardTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public AuthGuardTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Protected_Endpoint_Should_Return_Unauthorized_Without_Token()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
