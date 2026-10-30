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
using SESMT.API.Contracts.Browser;
using SESMT.API.Controllers;
using SESMT.API.Services;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence;
using OrganizationUnit = SESMT.Domain.Modules.Organization.Entities.Unit;

namespace SESMT.Tests.Integration.API;

// Real HTTP routes, JWT validation, EF reader and JSON contract; isolated EF InMemory persistence.
// PostgreSQL query execution and deployment are separate integration checks.
public sealed class BrowserWorkersHttpTests
{
    [Fact]
    public async Task ReadRequiresAuthenticationAndCurrentGrant_AndNeverEnablesWrites()
    {
        await using var host = await WorkerHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(host.ListUrl)).StatusCode);
        host.SignIn();
        host.Access.Current = host.Access.Current! with { Grants = [] };
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(host.ListUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await host.Client.PostAsJsonAsync("/api/v1/browser/workers", new { fullName = "Unrequested write" })).StatusCode);
    }

    [Fact]
    public async Task ListFiltersBeforeCountingAndPaging_AndMapsNativeFieldsWithoutInventedState()
    {
        await using var host = await WorkerHost.StartAsync();
        var alice = host.Worker("Alice Souza", "M-100");
        var bruno = host.Worker("Bruno Lima", "M-200", dismissal: new DateOnly(2020, 1, 1));
        var carla = host.Worker("Carla Alves", "M-300");
        var future = host.Worker("Daniel Farias", "M-400", dismissal: new DateOnly(2099, 1, 1));
        var removed = host.Worker("Elisa Costa", "M-500");
        removed.Deactivate();
        var foreign = host.Worker("Fora da unidade", "M-600", unit: host.OtherUnit.Id, sector: host.OtherSector.Id);
        await host.Seed(alice, bruno, carla, future, removed, foreign);

        var response = await host.Client.GetAsync(host.ListUrl + "&status=ACTIVE&page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        var row = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(carla.Id.ToString(), row.GetProperty("id").GetString());
        Assert.True(row.GetProperty("sourceReadOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("employmentType").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("revision").ValueKind);
        Assert.False(row.TryGetProperty("cpfEncryption", out _));
        Assert.False(row.TryGetProperty("cpfHash", out _));
        Assert.False(row.TryGetProperty("dateOfBirth", out _));
        Assert.False(row.TryGetProperty("integration", out _));

        var search = await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerResponse>>(host.ListUrl + "&q=m-200&status=TERMINATED");
        Assert.Equal(bruno.Id, Assert.Single(search!.Items).Id);
        var byName = await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerResponse>>(host.ListUrl + "&q=alice");
        Assert.Equal(alice.Id, Assert.Single(byName!.Items).Id);
    }

    [Fact]
    public async Task SectorScope_DoesNotExpandToNeighbouringOrUnassignedWorkers_OrInconsistentHierarchy()
    {
        await using var host = await WorkerHost.StartAsync(fullUnit: false);
        var permitted = host.Worker("Ana Araujo", "S-100");
        var neighbour = host.Worker("Beatriz Araujo", "S-200", sector: host.NeighbourSector.Id);
        var unassigned = host.Worker("Clara Araujo", "S-300", noSector: true);
        var foreignCompany = host.Worker("Dora Araujo", "S-400", company: Guid.NewGuid());
        var incorrectParent = host.Worker("Eva Araujo", "S-500", unit: host.OtherUnit.Id, sector: host.Sector.Id);
        await host.Seed(permitted, neighbour, unassigned, foreignCompany, incorrectParent);

        var list = await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerResponse>>(host.ListUrl);
        Assert.Equal(1, list!.Total);
        Assert.Equal(permitted.Id, Assert.Single(list.Items).Id);
        foreach (var hidden in new[] { neighbour, unassigned, foreignCompany, incorrectParent })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/workers/{hidden.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/workers/{hidden.Id}/movements")).StatusCode);
        }
    }

    [Fact]
    public async Task FullUnitScope_IncludesWorkerWithoutSector_WithoutFabricatingAssignment()
    {
        await using var host = await WorkerHost.StartAsync();
        var worker = host.Worker("Sem Setor", "N-100", noSector: true);
        await host.Seed(worker);
        var response = await host.Client.GetFromJsonAsync<JsonElement>($"/api/v1/browser/workers/{worker.Id}");
        Assert.Equal(JsonValueKind.Null, response.GetProperty("sectorId").ValueKind);
        Assert.Equal(worker.Id.ToString(), response.GetProperty("id").GetString());
    }

    [Fact]
    public async Task OwnOnly_RestrictsListDetailAndHistoryToLinkedWorker_EvenWithinSameSector()
    {
        await using var host = await WorkerHost.StartAsync();
        var own = host.Worker("Proprio Trabalhador", "O-100");
        var other = host.Worker("Outro Trabalhador", "O-200");
        await host.Seed(own, other);
        host.Access.Current = host.Access.Current! with { OwnOnly = true, WorkerId = own.Id };
        var page = await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerResponse>>(host.ListUrl);
        Assert.Equal(own.Id, Assert.Single(page!.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/api/v1/browser/workers/{own.Id}/movements")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/workers/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/workers/{other.Id}/movements")).StatusCode);
        host.Access.Current = host.Access.Current! with { WorkerId = null };
        Assert.Empty((await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerResponse>>(host.ListUrl))!.Items);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=1&page=2")]
    [InlineData("companyId=invalid")]
    [InlineData("teamId=unimplemented")]
    [InlineData("integrationStatus=PENDING")]
    [InlineData("status=ON_LEAVE")]
    [InlineData("status=unknown")]
    [InlineData("q=ab%0Acd")]
    [InlineData("q=%09")]
    [InlineData("unexpectedFilter=value")]
    public async Task InvalidOrUnsupportedFilters_AreRejectedBeforeDataRead(string filter)
    {
        await using var host = await WorkerHost.StartAsync();
        var result = await host.Client.GetAsync(host.ListUrl + "&" + filter);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await result.Content.ReadFromJsonAsync<BrowserErrorResponse>())!.Code);
        Assert.Equal(0, host.Reader.ListCalls);
    }

    [Fact]
    public async Task MissingInvalidOrRepeatedUnit_IsRejected_AndForeignUnitAndCompanyAreForbidden()
    {
        await using var host = await WorkerHost.StartAsync();
        foreach (var query in new[] { "", "?unitId=invalid", "?unitId=00000000-0000-0000-0000-000000000000", $"?unitId={host.Unit.Id}&unitId={host.Unit.Id}" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await host.Client.GetAsync("/api/v1/browser/workers" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/workers?unitId={host.OtherUnit.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(host.ListUrl + $"&companyId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await host.Client.GetAsync(host.ListUrl + "&q=" + new string('a', 201))).StatusCode);
        Assert.Equal(0, host.Reader.ListCalls);
    }

    [Fact]
    public async Task History_UsesRealHistoricalAssignment_SortsAndScopesBeforePaging()
    {
        await using var host = await WorkerHost.StartAsync(fullUnit: false);
        var worker = host.Worker("Historico Trabalhador", "H-100");
        await host.Seed(worker);
        var first = new WorkerHistory(worker.Id, host.Sector.Id, host.Position.Id, new DateOnly(2022, 1, 1));
        var second = new WorkerHistory(worker.Id, host.Sector.Id, host.Position.Id, new DateOnly(2023, 1, 1), reasonForChange: "Mudança registrada");
        var neighbour = new WorkerHistory(worker.Id, host.NeighbourSector.Id, host.Position.Id, new DateOnly(2024, 1, 1));
        var foreign = new WorkerHistory(worker.Id, host.OtherSector.Id, host.Position.Id, new DateOnly(2025, 1, 1));
        await host.Seed(first, second, neighbour, foreign);
        var response = await host.Client.GetFromJsonAsync<BrowserWorkerPage<BrowserWorkerMovementResponse>>(
            $"/api/v1/browser/workers/{worker.Id}/movements?page=2&pageSize=1&sort=effectiveDate:desc");
        Assert.Equal(2, response!.Total);
        var movement = Assert.Single(response.Items);
        Assert.Equal(first.Id, movement.Id);
        Assert.Equal("ASSIGNMENT", movement.Type);
        Assert.Null(movement.PreviousAssignment);
        Assert.Equal(host.Unit.Id, movement.NewAssignment.UnitId);
        Assert.Equal(host.Sector.Id, movement.NewAssignment.SectorId);
        Assert.Null(movement.Reason);
        Assert.Equal(first.CreatedAt, movement.CreatedAt);
        Assert.Null(movement.CreatedBy);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await host.Client.GetAsync($"/api/v1/browser/workers/{worker.Id}/movements?sort=effectiveDate:asc")).StatusCode);
    }

    private sealed class AccessReader(BrowserWorkspaceAccess access) : IBrowserWorkspaceAccessReader
    {
        public BrowserWorkspaceAccess? Current { get; set; } = access;
        public Task<BrowserWorkspaceAccess?> ReadAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(Current?.UserId == userId ? Current : null);
    }

    private sealed class CountingReader(BrowserWorkersReader inner) : IBrowserWorkersReader
    {
        public int ListCalls { get; private set; }
        public Task<BrowserWorkerPage<BrowserWorkerResponse>> ListAsync(BrowserWorkspaceAccess access, BrowserWorkerListQuery query, CancellationToken ct)
        { ListCalls++; return inner.ListAsync(access, query, ct); }
        public Task<BrowserWorkerResponse?> FindAsync(BrowserWorkspaceAccess access, Guid id, CancellationToken ct) => inner.FindAsync(access, id, ct);
        public Task<BrowserWorkerPage<BrowserWorkerMovementResponse>> MovementsAsync(BrowserWorkspaceAccess access, BrowserWorkerResponse worker, int page, int pageSize, CancellationToken ct) =>
            inner.MovementsAsync(access, worker, page, pageSize, ct);
    }

    private sealed class WorkerHost(WebApplication app, SesmtDbContext db, HttpClient client, AccessReader access, CountingReader reader,
        Guid companyId, OrganizationUnit unit, OrganizationUnit otherUnit, Department sector, Department neighbourSector, Department otherSector, Position position) : IAsyncDisposable
    {
        private const string SigningKey = "workers-test-only-signing-key-with-at-least-32-bytes";
        public HttpClient Client { get; } = client;
        public AccessReader Access { get; } = access;
        public CountingReader Reader { get; } = reader;
        public OrganizationUnit Unit { get; } = unit;
        public OrganizationUnit OtherUnit { get; } = otherUnit;
        public Department Sector { get; } = sector;
        public Department NeighbourSector { get; } = neighbourSector;
        public Department OtherSector { get; } = otherSector;
        public Position Position { get; } = position;
        public string ListUrl => $"/api/v1/browser/workers?unitId={Unit.Id}";

        public static async Task<WorkerHost> StartAsync(bool fullUnit = true)
        {
            var company = Guid.NewGuid();
            var unit = NewUnit(company, "Matriz");
            var otherUnit = NewUnit(company, "Filial");
            var sector = new Department(unit.Id, "Manutenção", null, null);
            var neighbour = new Department(unit.Id, "Produção", null, null);
            var otherSector = new Department(otherUnit.Id, "Logística", null, null);
            var position = new Position(company, "Técnico", null, null);
            var access = new AccessReader(new(Guid.NewGuid(), [company], [unit.Id], fullUnit ? [sector.Id, neighbour.Id] : [sector.Id],
                [], fullUnit ? [unit.Id] : [], false, null, [new("workers.read", company)],
                new Dictionary<Guid, Guid> { [unit.Id] = company }, fullUnit
                    ? new Dictionary<Guid, Guid> { [sector.Id] = unit.Id, [neighbour.Id] = unit.Id }
                    : new Dictionary<Guid, Guid> { [sector.Id] = unit.Id }));
            var db = new SesmtDbContext(new DbContextOptionsBuilder<SesmtDbContext>().UseInMemoryDatabase($"browser-workers-{Guid.NewGuid()}").Options);
            db.AddRange(unit, otherUnit, sector, neighbour, otherSector, position);
            await db.SaveChangesAsync();
            var reader = new CountingReader(new BrowserWorkersReader(db));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IBrowserWorkspaceAccessReader>(access);
            builder.Services.AddSingleton<IBrowserWorkersReader>(reader);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "workers-tests", ValidateAudience = true, ValidAudience = "browser-workers",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(BrowserWorkersController).Assembly);
            var app = builder.Build();
            app.UsePathBase("/api/v1");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var host = new WorkerHost(app, db, app.GetTestClient(), access, reader, company, unit, otherUnit, sector, neighbour, otherSector, position);
            host.SignIn();
            return host;
        }

        public void SignIn()
        {
            var tokens = new JwtTokenService(Options.Create(new JwtSettings
            { SigningKey = SigningKey, Issuer = "workers-tests", Audience = "browser-workers", AccessTokenExpirationMinutes = 5 }));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(Access.Current!.UserId, [], []).Token);
        }
        public Worker Worker(string name, string registration, Guid? company = null, Guid? unit = null, Guid? sector = null, bool noSector = false, DateOnly? dismissal = null) =>
            new(company ?? companyId, registration, [1, 2, 3], $"test-hash-{Guid.NewGuid():N}", name, new DateOnly(1990, 1, 1),
                new DateOnly(2019, 1, 1), dismissal, Position.Id, unit ?? Unit.Id, noSector ? null : sector ?? Sector.Id, null);
        public async Task Seed(params object[] entities) { db.AddRange(entities); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); await db.DisposeAsync(); }
        private static OrganizationUnit NewUnit(Guid company, string name) => new(company, name, name, "MATRIZ", "87000000", "Rua Teste", "1", null, "Centro", "Maringá", "PR");
    }
}
