using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class NativeWorkerCreationPostgresTests(PostgresDatabaseFixture database)
{
    private async Task AllowCreateAsync()
    {
        await using var db = database.CreateContext();
        var role = new Role("QA_W_" + Guid.NewGuid().ToString("N"), "Cadastro fictício", "Somente teste isolado");
        db.AddRange(role, new UserRole(database.ActorA, role.Id, database.CompanyA));
        foreach (var action in new[] { "criar", "ler" })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == "trabalhador" && p.Action == action && p.Active && p.DeletedAt == null);
            if (permission is null) { permission = new Permission("trabalhador", action, "Teste isolado"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task CreatesProtectedWorkerAndConfirmsTheBrowserProfileWithoutIdentityFields()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        await AllowCreateAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var enrollment = "QA-" + Guid.NewGuid().ToString("N")[..20];
        using var reply = await client.PostAsJsonAsync("/api/v1/workers", new { companyId = database.CompanyA, unitId = database.UnitA,
            enrollment, name = "Pessoa fictícia do cadastro", cpf = "00000000191", dateOfBirth = "1990-01-01", admissionDate = "2026-01-01" });
        var text = await reply.Content.ReadAsStringAsync();
        Assert.True(reply.StatusCode == HttpStatusCode.Created, text);
        using var parsed = JsonDocument.Parse(text); var id = parsed.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("***.***.***-91", parsed.RootElement.GetProperty("cpfMasked").GetString()); Assert.DoesNotContain("00000000191", text);
        await using var db = database.CreateContext(); var worker = await db.Workers.SingleAsync(w => w.Id == id);
        Assert.NotEmpty(worker.CpfEncryption); Assert.Equal(64, worker.CpfHash.Length); Assert.Null(worker.UserId);
        using var profile = await client.GetAsync($"/api/v1/browser/workers/{id}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = await profile.Content.ReadAsStringAsync(); Assert.DoesNotContain("dateOfBirth", body); Assert.DoesNotContain("00000000191", body);
        using var dto = JsonDocument.Parse(body); Assert.Equal(enrollment, dto.RootElement.GetProperty("registration").GetString()); Assert.Equal(database.UnitA, dto.RootElement.GetProperty("unitId").GetGuid());
    }

    [PostgresFact]
    public async Task CreationGrantInCompanyADoesNotCreateWorkersInCompanyB()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        await AllowCreateAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var enrollment = "QA-" + Guid.NewGuid().ToString("N")[..20];
        using var denied = await client.PostAsJsonAsync("/api/v1/workers", new { companyId = database.CompanyB, unitId = database.UnitB,
            enrollment, name = "Pessoa fora do alcance", cpf = "00000000272", dateOfBirth = "1990-01-01", admissionDate = "2026-01-01" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var db = database.CreateContext(); Assert.False(await db.Workers.AnyAsync(w => w.Enrollment == enrollment));
    }
}
