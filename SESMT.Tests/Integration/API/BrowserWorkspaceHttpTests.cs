using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SESMT.API.Controllers;
using SESMT.API.Services;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Common.Entity;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;
using OrganizationUnit = SESMT.Domain.Modules.Organization.Entities.Unit;

namespace SESMT.Tests.Integration.API;

// Signed JWT + real HTTP routing + actual repositories/DbContext; the isolated EF store is not PostgreSQL.
public sealed class BrowserWorkspaceHttpTests
{
    [Fact]
    public async Task CompanyScope_ExpandsActiveHierarchyAndExposesOnlyImplementedReadPermissions()
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Scope("EMPRESA", host.Company.Id);
        await host.Db.SaveChangesAsync();
        var identity = await host.JsonAsync("/api/v1/browser/me");
        Assert.Equal(new[] { "documents.read", "organization.read", "workers.read" },
            identity.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { host.Company.Id }, Ids(identity.GetProperty("scope"), "companyIds", false));
        Assert.Equal(new[] { host.Unit.Id, host.OtherUnit.Id }.Order(), Ids(identity.GetProperty("scope"), "unitIds", false).Order());
        Assert.Equal(new[] { host.Sector.Id, host.OtherSector.Id }.Order(), Ids(identity.GetProperty("scope"), "sectorIds", false).Order());
        var organization = await host.JsonAsync("/api/v1/browser/organization");
        Assert.Equal(new[] { host.Company.Id }, Ids(organization, "companies"));
        Assert.Equal(2, organization.GetProperty("units").GetArrayLength());
        Assert.Equal(new[] { host.Position.Id }, Ids(organization, "jobs"));
        Assert.Empty(organization.GetProperty("establishments").EnumerateArray());
        Assert.Empty(organization.GetProperty("jobFunctions").EnumerateArray());
        Assert.Empty(organization.GetProperty("workCenters").EnumerateArray());
        Assert.Equal("INTERNAL", organization.GetProperty("companies")[0].GetProperty("classification").GetString());
        Assert.Equal("12345678000195", organization.GetProperty("companies")[0].GetProperty("taxId").GetString());
        Assert.Equal(JsonValueKind.Null, organization.GetProperty("companies")[0].GetProperty("revision").ValueKind);
        Assert.True(organization.GetProperty("companies")[0].GetProperty("sourceReadOnly").GetBoolean());
    }

    [Theory]
    [InlineData("UNIDADE")]
    [InlineData("SETOR")]
    public async Task NarrowScope_ReturnsAncestorsWithoutOpeningSiblingLocations(string scope)
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Scope(scope, scope == "UNIDADE" ? host.Unit.Id : host.Sector.Id);
        await host.Db.SaveChangesAsync();
        var organization = await host.JsonAsync("/api/v1/browser/organization");
        Assert.Equal(new[] { host.Company.Id }, Ids(organization, "companies"));
        Assert.Equal(new[] { host.Unit.Id }, Ids(organization, "units"));
        Assert.Equal(new[] { host.Sector.Id }, Ids(organization, "sectors"));
        var access = await host.AccessAsync();
        Assert.True(access.AllowsLocation(host.Company.Id, host.Unit.Id, host.Sector.Id));
        Assert.False(access.AllowsLocation(host.ForeignCompany.Id, host.Unit.Id, host.Sector.Id));
        Assert.False(access.AllowsLocation(host.Company.Id, host.Unit.Id, host.OtherSector.Id));
        Assert.False(access.AllowsLocation(host.Company.Id, host.OtherUnit.Id));
        Assert.False(access.AllowsLocation(host.Company.Id, null));
        Assert.Equal(scope == "UNIDADE", access.AllowsLocation(host.Company.Id, host.Unit.Id));
    }

    [Fact]
    public async Task CompanySpecificGrant_DoesNotLeakAcrossAnotherAuthorizedScope()
    {
        await using var host = await WorkspaceHost.StartAsync(companyBoundRole: true);
        host.Scope("EMPRESA", host.Company.Id);
        host.Scope("EMPRESA", host.ForeignCompany.Id);
        await host.Db.SaveChangesAsync();
        var access = await host.AccessAsync();
        Assert.True(access.Can("organization.read", host.Company.Id));
        Assert.False(access.Can("organization.read", host.ForeignCompany.Id));
        var me = await host.JsonAsync("/api/v1/browser/me");
        Assert.DoesNotContain(host.ForeignCompany.Id, Ids(me.GetProperty("scope"), "companyIds", false));
        Assert.DoesNotContain(host.ForeignUnit.Id, Ids(me.GetProperty("scope"), "unitIds", false));
        var organization = await host.JsonAsync("/api/v1/browser/organization");
        Assert.Equal(new[] { host.Company.Id }, Ids(organization, "companies"));
        Assert.DoesNotContain(host.ForeignUnit.Id, Ids(organization, "units"));
        Assert.DoesNotContain(host.ForeignPosition.Id, Ids(organization, "jobs"));
    }

    [Fact]
    public async Task AdminTokenClaim_DoesNotGrantAccessWithoutActualGlobalAdminAssignment()
    {
        await using var host = await WorkspaceHost.StartAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
        Assert.Empty((await host.JsonAsync("/api/v1/browser/me")).GetProperty("permissions").EnumerateArray());
        // The JWT claimed ADMIN before and after this change; only the live DB assignment grants global scope.
        var admin = new Role("ADMIN", "Administrador", "Test role");
        host.Db.Roles.Add(admin);
        var assignment = new UserRole(host.User.Id, admin.Id);
        host.Db.UserRoles.Add(assignment);
        await host.Db.SaveChangesAsync();
        Assert.Equal(2, (await host.JsonAsync("/api/v1/browser/organization")).GetProperty("companies").GetArrayLength());
        assignment.MarkAsDeleted();
        await host.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("role")]
    [InlineData("assignment")]
    [InlineData("permission")]
    [InlineData("grant")]
    [InlineData("scope")]
    [InlineData("company")]
    public async Task DeletedAccessInputs_AreRecheckedOnEveryRequest(string deleted)
    {
        await using var host = await WorkspaceHost.StartAsync();
        var scope = host.Scope("EMPRESA", host.Company.Id);
        await host.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
        EntityBase record = deleted switch
        {
            "user" => host.User,
            "role" => host.Role,
            "assignment" => host.Assignment,
            "permission" => host.Db.Permissions.Single(item => item.Resource == "empresa"),
            "grant" => host.Db.RolePermissions.Single(item => item.PermissionId == host.Db.Permissions.Single(permission => permission.Resource == "empresa").Id),
            "scope" => scope,
            _ => host.Company
        };
        record.MarkAsDeleted();
        await host.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
        var meResponse = await host.Client.GetAsync("/api/v1/browser/me");
        if (deleted == "user") Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
        else
        {
            var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.DoesNotContain("organization.read", me.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
        }
    }

    [Fact]
    public async Task DisabledAncestorsAndSectors_DoNotReturnInIdentityOrOrganization()
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Scope("EMPRESA", host.Company.Id);
        host.Unit.MarkAsDeleted();
        host.OtherSector.MarkAsDeleted();
        await host.Db.SaveChangesAsync();
        var organization = await host.JsonAsync("/api/v1/browser/organization");
        Assert.Equal(new[] { host.OtherUnit.Id }, Ids(organization, "units"));
        Assert.Empty(Ids(organization, "sectors"));
        var access = await host.AccessAsync();
        Assert.False(access.HasUnit(host.Unit.Id));
        Assert.Empty(access.SectorIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("&unitId=bad")]
    public async Task Organization_RejectsMalformedOrDuplicateUnitFilters(string query)
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Scope("EMPRESA", host.Company.Id);
        await host.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await host.Client.GetAsync($"/api/v1/browser/organization?unitId={query}")).StatusCode);
    }

    [Fact]
    public async Task Organization_UnitFilterRespectsScopeAndDoesNotExposeOtherCompany()
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Scope("EMPRESA", host.Company.Id);
        await host.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/organization?unitId={host.ForeignUnit.Id}")).StatusCode);
        var organization = await host.JsonAsync($"/api/v1/browser/organization?unitId={host.Unit.Id}");
        Assert.Equal(new[] { host.Unit.Id }, Ids(organization, "units"));
        Assert.Equal(new[] { host.Sector.Id }, Ids(organization, "sectors"));
    }

    [Fact]
    public async Task WorkerOnlyIdentity_IsRestrictedToUniqueRealAllocationAndCannotReadGlobalOrganization()
    {
        await using var host = await WorkspaceHost.StartAsync(workerOnly: true);
        host.Scope("EMPRESA", host.Company.Id);
        host.Scope("EMPRESA", host.ForeignCompany.Id);
        var worker = host.LinkWorker();
        await host.Db.SaveChangesAsync();
        var identity = await host.JsonAsync("/api/v1/browser/me");
        Assert.True(identity.GetProperty("scope").GetProperty("ownOnly").GetBoolean());
        Assert.Equal(worker.Id, identity.GetProperty("workerId").GetGuid());
        Assert.Equal(new[] { host.Company.Id }, Ids(identity.GetProperty("scope"), "companyIds", false));
        Assert.Equal(new[] { host.Unit.Id }, Ids(identity.GetProperty("scope"), "unitIds", false));
        Assert.DoesNotContain("organization.read", identity.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
        host.LinkWorker("worker-duplicate");
        await host.Db.SaveChangesAsync();
        Assert.Null(await host.Reader.ReadAsync(host.User.Id, default));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/me")).StatusCode);
    }

    [Fact]
    public async Task WorkerAllocation_MustMatchCompanyAndSectorAncestors()
    {
        await using var host = await WorkspaceHost.StartAsync(workerOnly: true);
        host.Scope("EMPRESA", host.Company.Id);
        host.Scope("EMPRESA", host.ForeignCompany.Id);
        var worker = host.LinkWorker();
        worker.ChangeAssignment(null, host.ForeignUnit.Id, null);
        await host.Db.SaveChangesAsync();
        Assert.Empty((await host.AccessAsync()).Permissions);
        worker.ChangeAssignment(null, host.Unit.Id, host.OtherSector.Id);
        await host.Db.SaveChangesAsync();
        Assert.Empty((await host.AccessAsync()).Permissions);
    }

    [Fact]
    public async Task Organization_RequiresAuthenticatedSignedBearer()
    {
        await using var host = await WorkspaceHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "unsigned");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/organization")).StatusCode);
    }

    private static Guid[] Ids(JsonElement root, string property, bool objects = true) => root.GetProperty(property)
        .EnumerateArray().Select(value => objects ? value.GetProperty("id").GetGuid() : value.GetGuid()).ToArray();

    private sealed class WorkspaceHost(WebApplication app, HttpClient client, SesmtDbContext db, User user, Role role,
        UserRole assignment, Company company, Company foreignCompany, OrganizationUnit unit, OrganizationUnit otherUnit, OrganizationUnit foreignUnit,
        Department sector, Department otherSector, Position position, Position foreignPosition) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public SesmtDbContext Db { get; } = db;
        public User User { get; } = user;
        public Role Role { get; } = role;
        public UserRole Assignment { get; } = assignment;
        public Company Company { get; } = company;
        public Company ForeignCompany { get; } = foreignCompany;
        public OrganizationUnit Unit { get; } = unit;
        public OrganizationUnit OtherUnit { get; } = otherUnit;
        public OrganizationUnit ForeignUnit { get; } = foreignUnit;
        public Department Sector { get; } = sector;
        public Department OtherSector { get; } = otherSector;
        public Position Position { get; } = position;
        public Position ForeignPosition { get; } = foreignPosition;
        public IBrowserWorkspaceAccessReader Reader => new BrowserWorkspaceAccessReader(new UserAuthenticationRepository(Db), Db);
        public async Task<BrowserWorkspaceAccess> AccessAsync() => Assert.IsType<BrowserWorkspaceAccess>(await Reader.ReadAsync(User.Id, default));
        public UserScope Scope(string kind, Guid id)
        {
            var scope = new UserScope(User.Id, kind);
            scope.Update(User.Id, kind, id);
            Db.UserScopes.Add(scope);
            return scope;
        }
        public Worker LinkWorker(string enrollment = "worker-1")
        {
            var worker = new Worker(Company.Id, enrollment, [1], enrollment, "Pessoa Teste", new(1990, 1, 1),
                new(2025, 1, 1), null, Position.Id, Unit.Id, Sector.Id, User.Id);
            Db.Workers.Add(worker);
            return worker;
        }
        public async Task<JsonElement> JsonAsync(string path)
        {
            var response = await Client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public static async Task<WorkspaceHost> StartAsync(bool companyBoundRole = false, bool workerOnly = false)
        {
            const string signingKey = "workspace-integration-only-signing-key-with-at-least-32-bytes";
            var db = new SesmtDbContext(new DbContextOptionsBuilder<SesmtDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var user = new User("Pessoa Teste", "workspace@example.test", "not-a-real-hash");
            var company = new Company("Empresa A Ltda", "Empresa A", "12345678000195", null, null, CompanyType.INTERNA);
            var foreignCompany = new Company("Empresa B Ltda", "Empresa B", "11222333000181", null, null, CompanyType.PRESTADORA);
            static OrganizationUnit UnitFor(Company owner, string code) => new(owner.Id, code, code, "MATRIZ", "87000000", "Rua Teste", "1", null, "Centro", "Maringá", "PR");
            var unit = UnitFor(company, "A1"); var otherUnit = UnitFor(company, "A2"); var foreignUnit = UnitFor(foreignCompany, "B1");
            var sector = new Department(unit.Id, "Setor A1", "Setor visível", null);
            var otherSector = new Department(otherUnit.Id, "Setor A2", "Setor irmão", null);
            var position = new Position(company.Id, "Cargo A", "1234", "Cargo autorizado");
            var foreignPosition = new Position(foreignCompany.Id, "Cargo B", "5678", "Outra empresa");
            var role = new Role(workerOnly ? "TRABALHADOR" : "GESTOR", "Perfil", "Perfil de teste");
            var assignment = new UserRole(user.Id, role.Id, companyBoundRole ? company.Id : null);
            db.AddRange(user, company, foreignCompany, unit, otherUnit, foreignUnit, sector, otherSector, position, foreignPosition, role, assignment);
            foreach (var resource in new[] { "empresa", "unidade", "setor", "cargo", "trabalhador", "documento" })
            {
                var permission = new Permission(resource, "ler", "Consultar");
                db.AddRange(permission, new RolePermission(role.Id, permission.Id));
            }
            await db.SaveChangesAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(db);
            builder.Services.AddScoped<IUserAuthenticationRepository, UserAuthenticationRepository>();
            builder.Services.AddScoped<IBrowserWorkspaceAccessReader, BrowserWorkspaceAccessReader>();
            builder.Services.AddScoped<IBrowserIdentityReader, BrowserIdentityReader>();
            var sessions = new UnusedSessionServices();
            builder.Services.AddSingleton<ILoginService>(sessions);
            builder.Services.AddSingleton<IRefreshSessionService>(sessions);
            builder.Services.AddSingleton<ILogoutService>(sessions);
            builder.Services.AddAntiforgery();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "workspace-tests", ValidateAudience = true, ValidAudience = "workspace-browser",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(BrowserOrganizationController).Assembly);
            var app = builder.Build(); app.UsePathBase("/api/v1");
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
            var client = app.GetTestClient();
            // Deliberately broader token role: the DB remains authoritative for browser grants.
            var token = new JwtTokenService(Options.Create(new JwtSettings
            { SigningKey = signingKey, Issuer = "workspace-tests", Audience = "workspace-browser", AccessTokenExpirationMinutes = 5 }))
                .GenerateAccessToken(user.Id, ["ADMIN"], Array.Empty<UserScopeClaim>());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            return new(app, client, db, user, role, assignment, company, foreignCompany, unit, otherUnit, foreignUnit, sector, otherSector, position, foreignPosition);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); await Db.DisposeAsync(); }
    }

    private sealed class UnusedSessionServices : ILoginService, IRefreshSessionService, ILogoutService
    {
        public Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken ct = default) => throw new InvalidOperationException("Not used by readonly tests");
        public Task<RefreshTokenResult> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default) => throw new InvalidOperationException("Not used by readonly tests");
        public Task<LogoutResult> ExecuteAsync(LogoutRequest request, CancellationToken ct = default) => throw new InvalidOperationException("Not used by readonly tests");
    }
}
