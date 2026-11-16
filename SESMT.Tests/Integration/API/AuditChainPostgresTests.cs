using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class AuditChainPostgresTests(PostgresDatabaseFixture database)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        await using var db = database.CreateContext(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString());
        return client;
    }

    [PostgresFact]
    public async Task ConcurrentAuditedWritesNeverLinkToTheSamePredecessor()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory);
        long start;
        await using (var db = database.CreateContext()) start = await db.AuditLogs.MaxAsync(a => (long?)a.Id) ?? 0;

        var replies = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => client.PostAsJsonAsync("/api/v1/trainings", new
        {
            unitId = database.UnitA, workerId = database.WorkerA, commandId = Guid.NewGuid(), title = "Curso concorrente " + (char)('A' + index),
            reference = (string?)null, kind = "INITIAL", durationMinutes = 60, completedOn = Today, renewalOn = (DateOnly?)null,
            confirmation = true, expectedRevision = (int?)null, changeReason = (string?)null,
        })));
        try { Assert.All(replies, reply => Assert.Equal(HttpStatusCode.Created, reply.StatusCode)); }
        finally { foreach (var reply in replies) reply.Dispose(); }

        await using var check = database.CreateContext();
        var rows = await check.AuditLogs.AsNoTracking().Where(a => a.Id > start).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.PreviousHash, a.CurrentHash }).ToListAsync();
        Assert.True(rows.Count >= 16);
        var predecessor = await check.AuditLogs.AsNoTracking().Where(a => a.Id <= start).OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync();
        // A single unbroken line: each new event points at the one stored right before it.
        foreach (var row in rows)
        {
            Assert.Equal(predecessor, row.PreviousHash);
            predecessor = row.CurrentHash;
        }
        Assert.All(await check.AuditLogs.Where(a => a.Id > start).ToListAsync(), log => Assert.True(log.IsIntegrityValid()));
    }

    [PostgresFact]
    public async Task OnlyAGlobalAdministratorWithAuditAccessCanVerifyTheChain()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory);
        using var allowed = await admin.GetAsync("/api/v1/audit-logs/chain-integrity");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var report = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        Assert.True(report.RootElement.GetProperty("checkedRows").GetInt64() > 0);
        Assert.Contains(report.RootElement.GetProperty("intact").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
        Assert.DoesNotContain("actorEmail", report.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);

        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var user = new User("Auditoria da empresa", key + "@example.invalid", "fixture-only-hash");
        var role = new Role("QA_AUD_" + key[..20], "Auditoria da empresa", "Consulta isolada de teste");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA),
            new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "auditoria" && p.Action == "ler")).Id));
        await db.SaveChangesAsync();
        using var companyAuditor = factory.CreateClient(); companyAuditor.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString());
        using var denied = await companyAuditor.GetAsync("/api/v1/audit-logs/chain-integrity");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
