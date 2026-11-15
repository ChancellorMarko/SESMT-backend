using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SESMT.API.Contracts.Errors;

namespace SESMT.API.Configuration;

public static class RateLimitPolicies
{
    /// <summary>Login, refresh and password reset: limits credential stuffing and reset-mail abuse per client address.</summary>
    public const string Authentication = "authentication";
    // Silent session renewal runs for every signed-in tab; it must not share the login bucket,
    // or a whole office behind one public IP would be signed out once it passes the login limit.
    public const string SessionRefresh = "session-refresh";
    /// <summary>Anonymous appointment invite endpoints, reachable by anyone holding a link.</summary>
    public const string PublicInvite = "public-invite";
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";
    public bool Enabled { get; set; } = true;
    public int AuthenticationPerMinute { get; set; } = 20;
    public int SessionRefreshPerMinute { get; set; } = 600;
    public int PublicInvitePerMinute { get; set; } = 30;
}

public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173", "http://localhost:4173"];
}

public static class HttpProtection
{
    public static IServiceCollection AddHttpProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var limits = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();
        // Integration tests authenticate many times from one in-memory client; they opt in explicitly.
        var enabled = limits.Enabled && (!environment.IsEnvironment("Testing") || configuration.GetValue<bool>("RateLimiting:EnabledInTesting"));
        if (limits.AuthenticationPerMinute < 1 || limits.SessionRefreshPerMinute < 1 || limits.PublicInvitePerMinute < 1)
            throw new InvalidOperationException("Os limites de requisição por minuto devem ser maiores que zero.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse("TOO_MANY_REQUESTS",
                    "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente.", context.HttpContext.TraceIdentifier), ct);
            };
            options.AddPolicy(RateLimitPolicies.Authentication, context => Partition(context, enabled, limits.AuthenticationPerMinute));
            options.AddPolicy(RateLimitPolicies.SessionRefresh, context => Partition(context, enabled, limits.SessionRefreshPerMinute));
            options.AddPolicy(RateLimitPolicies.PublicInvite, context => Partition(context, enabled, limits.PublicInvitePerMinute));
        });

        var origins = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins ?? new CorsSettings().AllowedOrigins;
        if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || origin.EndsWith('/')))
            throw new InvalidOperationException("Cors:AllowedOrigins deve listar origens absolutas, sem caminho nem barra final.");
        services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .WithOrigins(origins).AllowAnyHeader().WithExposedHeaders("X-Total-Count").AllowAnyMethod().AllowCredentials()));
        return services;
    }

    // Partitioned by client address. Behind a reverse proxy, configure forwarded headers so this is the real client.
    private static RateLimitPartition<string> Partition(HttpContext context, bool enabled, int perMinute) => enabled
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("disabled");

    /// <summary>Headers that cost nothing for a JSON API: no MIME sniffing, no framing, no referrer leakage.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
        await next();
    });
}
