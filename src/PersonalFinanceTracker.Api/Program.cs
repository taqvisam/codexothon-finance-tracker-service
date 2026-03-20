using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using PersonalFinanceTracker.Api.Middleware;
using PersonalFinanceTracker.Infrastructure;
using PersonalFinanceTracker.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"];
if (string.IsNullOrWhiteSpace(configuredUrls))
{
    var azurePort = Environment.GetEnvironmentVariable("PORT") ?? Environment.GetEnvironmentVariable("WEBSITES_PORT");
    if (!string.IsNullOrWhiteSpace(azurePort))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{azurePort}");
    }
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "OAuth2 JWT Authorization header using Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
var allowedOrigins = configuredOrigins
    .SelectMany(origin =>
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return new[] { origin };
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            var alt = $"{uri.Scheme}://127.0.0.1:{uri.Port}";
            return new[] { origin, alt };
        }

        if (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            var alt = $"{uri.Scheme}://localhost:{uri.Port}";
            return new[] { origin, alt };
        }

        return new[] { origin };
    })
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var allowedHostSuffixes = new[]
{
    ".azurestaticapps.net",
    ".ngrok-free.dev"
};
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    if (allowedHostSuffixes.Any(suffix =>
                            uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }

                    var isLocalHost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
                    if (isLocalHost && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        return true;
                    }
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "ChangeThisInProduction_AtLeast32Chars";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PersonalFinanceTracker";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PersonalFinanceTrackerClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("auth-sensitive", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrationRetryCount = Math.Max(1, app.Configuration.GetValue("Database:MigrateRetryCount", 10));
    var migrationRetryDelaySeconds = Math.Max(1, app.Configuration.GetValue("Database:MigrateRetryDelaySeconds", 5));

    for (var attempt = 1; attempt <= migrationRetryCount; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
            logger.LogInformation("Database migration succeeded.");
            break;
        }
        catch (Exception ex) when (attempt < migrationRetryCount)
        {
            logger.LogWarning(
                ex,
                "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds} seconds.",
                attempt,
                migrationRetryCount,
                migrationRetryDelaySeconds);

            await Task.Delay(TimeSpan.FromSeconds(migrationRetryDelaySeconds), app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database startup check failed. Ensure PostgreSQL is running and the connection string is valid.");
            throw new InvalidOperationException(
                "Startup failed: database is unavailable or migration failed. " +
                "Start PostgreSQL and verify ConnectionStrings:Postgres before running the API.",
                ex);
        }
    }

    var demoSeedEnabled = app.Configuration.GetValue("DemoSeed:Enabled", true);
    if (demoSeedEnabled)
    {
        try
        {
            await DemoDataSeeder.SeedAsync(db, app.Lifetime.ApplicationStopping);
            logger.LogInformation("Demo data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo data seeding failed. API startup will continue without seeded data.");
        }
    }
    else
    {
        logger.LogInformation("Demo data seeding is disabled (DemoSeed:Enabled=false).");
    }

    try
    {
        var seededCount = await DefaultCategorySeeder.SeedAsync(db, app.Lifetime.ApplicationStopping);
        logger.LogInformation("Default category seeding ensured. Inserted {InsertedCount} missing categories.", seededCount);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Default category seeding failed. API startup will continue without default category backfill.");
    }
}

app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var databaseStatus = report.Entries.TryGetValue("database", out var dbEntry)
            ? dbEntry.Status.ToString()
            : "Unhealthy";

        var payload = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow.ToString("o"),
            services = new
            {
                api = report.Status.ToString(),
                database = databaseStatus
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.Run();

public partial class Program;
