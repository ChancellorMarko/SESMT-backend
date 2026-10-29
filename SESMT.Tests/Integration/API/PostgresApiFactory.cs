using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.Tests.Integration.API;

/// <summary>
/// Fábrica de WebApplicationFactory para os testes de integração.
/// Garante banco isolado, autenticação por X-Test-Actor e nenhuma
/// entrega externa de e-mail.
/// </summary>
public sealed class PostgresApiFactory(PostgresDatabaseFixture database)
    : WebApplicationFactory<Program>
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "sesmt-integration",
        Guid.NewGuid().ToString("N"));

    // ─── Configuração ───────────────────────────────────────────────────────

    private Dictionary<string, string?> Settings() => new()
    {
        // Banco isolado por fixture
        ["ConnectionStrings:Sesmt"] = database.ConnectionString,

        // JWT (32+ bytes obrigatórios)
        ["Jwt:SigningKey"] = "isolated-integration-tests-key-minimum-32-bytes",
        ["Jwt:Issuer"] = "sesmt-integration-tests",
        ["Jwt:Audience"] = "sesmt-integration-tests",

        // Seed inicial
        ["Seed:AdminEmail"] = "seed-admin@example.invalid",
        ["Seed:AdminName"] = "Administrador fictício dos testes",
        ["Seed:AdminPassword"] = "TestOnly-NotAnOperationalCredential-123!",

        // Logging mínimo
        ["Logging:LogLevel:Default"] = "Warning",

        // Storage em pasta temporária
        ["Storage:Local:RootPath"] = _storageRoot,
    };

    // ─── Ciclo de vida ──────────────────────────────────────────────────────

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Program.cs lê configurações de host imediatamente após CreateBuilder,
        // antes de qualquer ConfigureWebHost rodar. Por isso é preciso injetar
        // aqui também.
        builder.ConfigureHostConfiguration(
            configuration => configuration.AddInMemoryCollection(Settings()));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(Settings()));

        builder.ConfigureTestServices(services =>
        {
            // Autenticação por header X-Test-Actor (só nos testes)
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = FixtureAuthentication.SchemeName;
                    options.DefaultChallengeScheme = FixtureAuthentication.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>(
                    FixtureAuthentication.SchemeName, _ => { });

            // Nenhum teste pode causar entrega real de e-mail
            services.RemoveAll<IPasswordResetNotificationService>();
            services.AddSingleton<IPasswordResetNotificationService, RejectExternalNotifications>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Só apaga a raiz temporária criada por esta factory.
        var root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "sesmt-integration"))
            + Path.DirectorySeparatorChar;

        var owned = Path.GetFullPath(_storageRoot);

        if (Directory.Exists(owned)
            && owned.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(owned, recursive: true);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Fakes (classes aninhadas)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Serviço de notificação que NÃO envia e-mail e NÃO quebra o fluxo.
    /// O endpoint /auth/forgot-password precisa retornar 202 mesmo quando
    /// não há envio real (anti-enumeration).
    /// </summary>
    private sealed class RejectExternalNotifications
        : IPasswordResetNotificationService
    {
        public static int SendCalls { get; private set; }

        public Task SendAsync(
            string email,
            string token,
            CancellationToken cancellationToken = default)
        {
            SendCalls++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Authentication handler que autentica via header X-Test-Actor.
    /// Retorna NoResult (→ 401) quando o header está ausente ou inválido.
    /// </summary>
    public sealed class FixtureAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PostgresFixture";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var raw = Request.Headers["X-Test-Actor"].ToString();

            if (!Guid.TryParse(raw, out var actor) || actor == Guid.Empty)
                return Task.FromResult(AuthenticateResult.NoResult());

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actor.ToString()),
                        new Claim("sub", actor.ToString())
                    ],
                    SchemeName));

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
