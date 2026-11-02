using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SESMT.API.Controllers;
using SESMT.API.Middleware;
using SESMT.API.Services;
using SESMT.Application;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure;
using SESMT.Infrastructure.Persistence;
using SESMT.Tests.Unit.Identity;

namespace SESMT.Tests.Integration.API;

// Actual native controller, DI, authorization reader, persistence adapter and error pipeline.
// EF InMemory is intentional here; it does not replace relational PostgreSQL tests.
public sealed class NativeResourceHttpTests
{
    [Fact]
    public async Task WorkerRoutesRequireAuthenticationAndRejectForeignReadsAndWrites()
    {
        await using var host = await Host.Start();
        var f = host.Data;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync($"/api/v1/workers/{f.Workers[0].Id}")).StatusCode);
        host.SignIn();
        var foreign = f.Workers[3].Id;
        foreach (var path in new[] { $"workers/{foreign}", $"workers/{foreign}/history" })
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/v1/" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"/api/v1/workers/{foreign}")).StatusCode);
        var edit = await host.Client.PatchAsJsonAsync($"/api/v1/workers/{foreign}", new
        {
            enrollment = "Teste", name = "Pessoa Ficticia", dateOfBirth = "1990-01-01", admissionDate = "2020-01-01"
        });
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(0, host.Cpf.Reads);
        Assert.Null(f.Workers[3].DeletedAt);
        var own = await host.Client.GetAsync($"/api/v1/workers/{f.Workers[0].Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(1, host.Cpf.Reads);
    }

    [Fact]
    public async Task NativeOrganisationAndTeamControllersUseGuardedInterfaces()
    {
        await using var host = await Host.Start(); host.SignIn(); var f = host.Data;
        foreach (var path in new[] { $"companies/{f.CompanyB.Id}", $"locations/{f.Units[2].Id}",
            $"departments/{f.Sectors[3].Id}", $"positions/{f.ForeignPosition.Id}" })
        {
            var response = await host.Client.GetAsync("/api/v1/" + path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("FORBIDDEN", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/teams/{f.ForeignTeam.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"/api/v1/teams/{f.ForeignTeam.Id}")).StatusCode);
        var list = await host.Client.GetFromJsonAsync<JsonElement>($"/api/v1/workers?companyId={f.CompanyA.Id}&pageSize=1&pageNumber=2");
        Assert.Equal(f.Workers[1].Id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task PendingRequirementErrorKeepsStructuredIssuesAndCorrelation()
    {
        await using var host = await Host.Start(); host.SignIn();
        var response = await host.Client.GetAsync("/api/v1/test/pending");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PENDING_REQUIREMENTS", body.GetProperty("code").GetString());
        Assert.Equal("DOCUMENTO_PENDENTE", Assert.Single(body.GetProperty("issues").EnumerateArray()).GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("requestId").GetString()));
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public NativeResourceAuthorizationTests.Fixture Data { get; }
        public HttpClient Client { get; }
        public CountingProtection Cpf { get; }
        private Host(WebApplication app, NativeResourceAuthorizationTests.Fixture data, CountingProtection cpf)
            => (_app, Data, Client, Cpf) = (app, data, app.GetTestClient(), cpf);

        public static async Task<Host> Start()
        {
            var data = await NativeResourceAuthorizationTests.Fixture.Create();
            var cpf = new CountingProtection();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Sesmt"] = "Host=127.0.0.1;Database=unused_native_http_tests" });
            builder.Services.AddControllers().AddApplicationPart(typeof(WorkersController).Assembly);
            builder.Services.AddApplication().AddInfrastructure(builder.Configuration).AddNativeResourceAuthorization();
            builder.Services.AddSingleton<SesmtDbContext>(data.Db);
            builder.Services.AddSingleton<ICpfProtectionService>(cpf);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IBrowserWorkspaceAccessReader, BrowserWorkspaceAccessReader>();
            builder.Services.AddAuthentication("NativeTest").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("NativeTest", _ => { });
            builder.Services.AddAuthorization();
            var app = builder.Build(); app.UsePathBase("/api/v1");
            app.UseMiddleware<ExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            app.MapGet("/test/pending", () => Task.FromException<IResult>(new WorkPermitNotReadyException(
                new WorkPermitReadinessAssessment(false, [new("DOCUMENTO_PENDENTE")]))));
            await app.StartAsync(); return new(app, data, cpf);
        }

        public void SignIn() => Client.DefaultRequestHeaders.Add("X-Test-Actor", Data.Actor.Id.ToString());
        public async ValueTask DisposeAsync() { Client.Dispose(); await _app.DisposeAsync(); await Data.DisposeAsync(); }
    }

    public sealed class HeaderAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Actor"], out var actor)) return Task.FromResult(AuthenticateResult.NoResult());
            // A nominal ADMIN claim must not promote the database's company-scoped actor.
            var identity = new ClaimsIdentity([new Claim("sub", actor.ToString()), new Claim(ClaimTypes.Role, "ADMIN")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class CountingProtection : ICpfProtectionService
    {
        public int Reads { get; private set; }
        public (byte[] Encrypted, string Hash) Protect(string value) => throw new NotSupportedException();
        public string Hash(string value) => throw new NotSupportedException();
        public string Unprotect(byte[] value) { Reads++; return "52998224725"; }
    }
}
