using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.Tests.Integration.API;

public sealed class PostgresApiFactory(PostgresDatabaseFixture database) : WebApplicationFactory<Program>
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "sesmt-integration", Guid.NewGuid().ToString("N"));
    private Dictionary<string, string?> Settings() => new()
        {
            ["ConnectionStrings:Sesmt"] = database.ConnectionString,
            ["Jwt:SigningKey"] = "isolated-integration-tests-key-minimum-32-bytes",
            ["Jwt:Issuer"] = "sesmt-integration-tests",
            ["Jwt:Audience"] = "sesmt-integration-tests",
            ["Seed:AdminEmail"] = "seed-admin@example.invalid",
            ["Seed:AdminName"] = "Administrador fictício dos testes",
            ["Seed:AdminPassword"] = "TestOnly-NotAnOperationalCredential-123!",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Storage:Local:RootPath"] = _storageRoot,
        };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Program reads connection/JWT settings immediately after CreateBuilder.
        // Host configuration is available before that code; app callbacks run later.
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings()));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings()));
        builder.ConfigureTestServices(services =>
        {
            // Authenticate a fixture actor, while real services still enforce roles and scopes in PostgreSQL.
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = FixtureAuthentication.SchemeName;
                options.DefaultChallengeScheme = FixtureAuthentication.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>(FixtureAuthentication.SchemeName, _ => { });
            // No test can cause an external e-mail delivery.
            services.RemoveAll<IPasswordResetNotificationService>();
            services.AddSingleton<IPasswordResetNotificationService>(new RejectExternalNotifications());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sesmt-integration")) + Path.DirectorySeparatorChar;
        var owned = Path.GetFullPath(_storageRoot);
        if (Directory.Exists(owned) && owned.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            Directory.Delete(owned, recursive: true);
    }

    private sealed class RejectExternalNotifications : IPasswordResetNotificationService
    {
        public Task SendAsync(string email, string token, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("External notification delivery is disabled in integration tests.");
    }

    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PostgresFixture";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Actor"], out var actor))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim("sub", actor.ToString()),
            ], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
