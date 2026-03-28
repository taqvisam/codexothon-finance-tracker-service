using PersonalFinanceTracker.Application.Services;

namespace PersonalFinanceTracker.Api.Middleware;

public sealed class SharedAccountAccessMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ManagedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "invite",
        "members",
        "activity"
    };

    public async Task Invoke(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await next(context);
            return;
        }

        if (TryGetManagedAccountRoute(context.Request.Path, out _))
        {
            var userId = context.User.GetUserId();
            if (userId == Guid.Empty)
            {
                throw new AppException("Unauthorized.", 401);
            }
        }

        await next(context);
    }

    private static bool TryGetManagedAccountRoute(PathString path, out Guid accountId)
    {
        accountId = Guid.Empty;
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments is null || segments.Length < 4)
        {
            return false;
        }

        if (!string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "accounts", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(segments[2], out accountId) ||
            !ManagedSegments.Contains(segments[3]))
        {
            accountId = Guid.Empty;
            return false;
        }

        return true;
    }
}
