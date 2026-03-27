using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IntegrationTests;

public class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:SkipStartupInitialization"] = "true",
                ["Database:SkipCriticalAuthSchemaBootstrap"] = "true",
                ["Database:MigrateRetryCount"] = "1",
                ["DemoSeed:Enabled"] = "false"
            });
        });
    }
}
