using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

// Exercise Program's actual PathBase, JWT events and MVC validation configuration.
// Unlike the controller HTTP hosts, this factory retains production authentication.
[Collection(PostgresCollection.Name)]
public sealed class BrowserProgramPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task VersionedBrowserChallenge_ReturnsStructuredErrorWhileNativeKeepsBearerChallenge()
    {
        await using var factory = new BrowserProgramFactory(database);
        using var client = factory.CreateClient();
        using var browser = await client.GetAsync("/api/v1/browser/me");
        Assert.Equal(HttpStatusCode.Unauthorized, browser.StatusCode);
        Assert.Equal("UNAUTHENTICATED", (await browser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.True(browser.Headers.CacheControl?.NoStore);

        using var native = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, native.StatusCode);
        Assert.Contains(native.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [PostgresFact]
    public async Task VersionedBrowserExpiredToken_ReturnsCodeNeededForAutomaticRefresh()
    {
        await using var factory = new BrowserProgramFactory(database);
        using var client = factory.CreateClient();
        var token = new JwtSecurityToken(
            issuer: BrowserProgramFactory.Issuer, audience: BrowserProgramFactory.Issuer,
            notBefore: DateTime.UtcNow.AddMinutes(-10), expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(BrowserProgramFactory.SigningKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/v1/browser/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task VersionedBrowserMalformedJson_UsesBrowserValidationWithoutChangingNativeProblemDetails()
    {
        await using var factory = new BrowserProgramFactory(database);
        using var client = factory.CreateClient();
        using var browser = await client.PostAsync("/api/v1/browser/auth/login", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, browser.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await browser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var native = await client.PostAsync("/api/v1/auth/login", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, native.StatusCode);
        Assert.Equal("application/problem+json", native.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task VersionedBrowserCsrf_SetsHttpOnlyCookieOnlyForBrowserNamespace()
    {
        await using var factory = new BrowserProgramFactory(database);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/browser/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrfToken").GetString()));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("ampara.csrf=", cookie);
        Assert.Contains("path=/api/v1/browser", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task VersionedBrowserReads_UseRealDatabaseGrantsAndKeepOtherCompanyDataIsolated()
    {
        await using var factory = new BrowserProgramFactory(database);
        using var client = factory.CreateClient();
        await using var db = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User("Leitor exclusivo do teste browser", $"browser-{suffix}@example.invalid", "not-a-login-hash");
        var role = new Role($"BROWSER_{suffix}", "Leitura browser isolada", "Somente para este cenário PostgreSQL");
        var scope = new UserScope(user.Id, "EMPRESA");
        scope.Update(user.Id, "EMPRESA", database.CompanyA);
        var typeId = await db.Documents.Where(document => document.Id == database.DocumentA).Select(document => document.DocumentTypeId).SingleAsync();
        var document = new Document(typeId, database.CompanyA, database.UnitA, null, $"BROWSER_{suffix}",
            "Documento exclusivo do teste browser", null, SecrecyLevel.Internal, DateOnly.FromDateTime(DateTime.UtcNow), null, null, user.Id);
        var file = new StoredFile("test-metadata-only", "isolated", suffix, "browser.pdf", "application/pdf", 24,
            new string('A', 64), true, AntiVirusScanResult.Clean);
        var version = new DocumentVersion(document.Id, 1, file.Id, file.FileHash, user.Id, "Versão fictícia sem download");
        try
        {
            var resources = new[] { "empresa", "unidade", "setor", "cargo", "trabalhador", "documento" };
            var permissions = await db.Permissions.Where(permission => resources.Contains(permission.Resource) && permission.Action == "ler"
                && permission.Active && permission.DeletedAt == null).ToArrayAsync();
            Assert.Equal(resources.Length, permissions.Length);
            var grants = permissions.Select(permission => new RolePermission(role.Id, permission.Id)).ToArray();
            db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA), document, file, version);
            db.AddRange(grants);
            await db.SaveChangesAsync();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BrowserProgramFactory.Token(user.Id));
            var me = await ReadOk(client, "/api/v1/browser/me");
            Assert.Equal(user.Id, me.GetProperty("user").GetProperty("id").GetGuid());
            Assert.Equal(new[] { "documents.read", "organization.read", "workers.read" },
                me.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray());
            Assert.Equal(database.CompanyA, Assert.Single(me.GetProperty("scope").GetProperty("companyIds").EnumerateArray()).GetGuid());

            var organization = await ReadOk(client, $"/api/v1/browser/organization?unitId={database.UnitA}");
            Assert.Equal(database.CompanyA, Assert.Single(organization.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
            Assert.Equal(database.UnitA, Assert.Single(organization.GetProperty("units").EnumerateArray()).GetProperty("id").GetGuid());
            var workers = await ReadOk(client, $"/api/v1/browser/workers?unitId={database.UnitA}&q=qa-0&status=ACTIVE&page=1&pageSize=10");
            Assert.Equal(database.WorkerA, Assert.Single(workers.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            Assert.Equal(database.CompanyA, (await ReadOk(client, $"/api/v1/browser/workers/{database.WorkerA}")).GetProperty("companyId").GetGuid());
            Assert.Equal(0, (await ReadOk(client, $"/api/v1/browser/workers/{database.WorkerA}/movements?page=1&pageSize=5")).GetProperty("total").GetInt32());

            var documents = await ReadOk(client, $"/api/v1/browser/documents?unitId={database.UnitA}&q={document.Code.ToLowerInvariant()}&status=DRAFT&page=1&pageSize=10");
            Assert.Equal(document.Id, Assert.Single(documents.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            Assert.Equal(document.Id, (await ReadOk(client, $"/api/v1/browser/documents/{document.Id}")).GetProperty("id").GetGuid());
            var versions = await ReadOk(client, $"/api/v1/browser/documents/{document.Id}/versions");
            Assert.Equal(version.Id, Assert.Single(versions.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            var versionDetail = await ReadOk(client, $"/api/v1/browser/documents/{document.Id}/versions/{version.Id}");
            Assert.Equal(file.FileName, versionDetail.GetProperty("file").GetProperty("name").GetString());
            Assert.Equal("CLEAN", versionDetail.GetProperty("file").GetProperty("scanStatus").GetString());
            Assert.Equal(0, (await ReadOk(client, $"/api/v1/browser/documents/{document.Id}/history")).GetProperty("total").GetInt32());
            Assert.Equal(typeId, (await ReadOk(client, $"/api/v1/browser/document-types/{typeId}")).GetProperty("id").GetGuid());

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/browser/documents?unitId={database.UnitB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/browser/documents/{database.DocumentB}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/browser/organization?unitId={database.UnitB}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/browser/workers?unitId={database.UnitB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/browser/workers/{database.WorkerB}")).StatusCode);

            var workerPermission = permissions.Single(permission => permission.Resource == "trabalhador").Id;
            await db.RolePermissions.Where(grant => grant.RoleId == role.Id && grant.PermissionId == workerPermission).ExecuteDeleteAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/browser/workers?unitId={database.UnitA}")).StatusCode);
        }
        finally
        {
            // Remove only this test's rows; other tests share the fixture's original actors and documents.
            await db.DocumentVersions.Where(item => item.Id == version.Id).ExecuteDeleteAsync();
            await db.Documents.Where(item => item.Id == document.Id).ExecuteDeleteAsync();
            await db.StoredFiles.Where(item => item.Id == file.Id).ExecuteDeleteAsync();
            await db.UserRoles.Where(item => item.UserId == user.Id).ExecuteDeleteAsync();
            await db.UserScopes.Where(item => item.UserId == user.Id).ExecuteDeleteAsync();
            await db.RolePermissions.Where(item => item.RoleId == role.Id).ExecuteDeleteAsync();
            await db.Roles.Where(item => item.Id == role.Id).ExecuteDeleteAsync();
            await db.Users.Where(item => item.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private static async Task<JsonElement> ReadOk(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    private sealed class BrowserProgramFactory(PostgresDatabaseFixture database) : WebApplicationFactory<Program>
    {
        public const string SigningKey = "program-browser-isolated-tests-key-minimum-32-bytes";
        public const string Issuer = "program-browser-tests";
        public static string Token(Guid userId) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer, audience: Issuer, claims: [new Claim("sub", userId.ToString())],
            notBefore: DateTime.UtcNow.AddSeconds(-1), expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        private Dictionary<string, string?> Settings() => new()
        {
            ["ConnectionStrings:Sesmt"] = database.ConnectionString,
            ["Jwt:SigningKey"] = SigningKey,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Issuer,
            ["Seed:AdminEmail"] = "seed-admin@example.invalid",
            ["Seed:AdminName"] = "Administrador fictício dos testes",
            ["Seed:AdminPassword"] = "TestOnly-NotAnOperationalCredential-123!",
            ["Logging:LogLevel:Default"] = "Warning",
        };

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings()));
        }
    }
}
