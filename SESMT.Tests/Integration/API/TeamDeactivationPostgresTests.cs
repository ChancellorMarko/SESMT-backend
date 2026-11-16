using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class TeamDeactivationPostgresTests(PostgresDatabaseFixture database)
{
    private async Task<HttpClient> ClientAsync(PostgresApiFactory factory, params string[] actions)
    {
        using var warmup = factory.CreateClient();
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var user = new User("Gestão de equipe", key + "@example.invalid", "fixture-only-hash");
        var role = new Role("QA_TD_" + key[..20], "Equipe de QA", "Somente teste isolado");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA));
        foreach (var action in actions)
            db.Add(new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "equipe" && p.Action == action && p.Active && p.DeletedAt == null)).Id));
        await db.SaveChangesAsync();
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString()); return client;
    }

    private async Task<Guid> CreateTeamAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/teams", new { organizationId = database.CompanyA, companyId = database.CompanyA,
            localId = database.UnitA, name = "QA-" + Guid.NewGuid().ToString("N"), description = (string?)null, chiefId = database.WorkerA, participantIds = Array.Empty<Guid>() });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static string TokenHash() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())));

    private WorkPermitAppointment Appointment(Guid team, DateTimeOffset startsAt) => WorkPermitAppointment.CreateRequest(database.CompanyA,
        "Agenda confidencial de QA", startsAt, startsAt.AddHours(2), database.ActorA, database.UnitA, team,
        [WorkPermitServiceCatalogEntry.All[0].Code], [database.WorkerA]);

    private static Task<HttpResponseMessage> Deactivate(HttpClient client, Guid team, int version, string reason = "Equipe reorganizada")
        => client.PostAsJsonAsync($"/api/v1/teams/{team}/deactivate", new { expectedVersion = version, reason });

    [PostgresFact]
    public async Task RequiresRevisionAndReasonRevokesInvitesAndAudits()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory, "ler", "criar", "excluir");
        var team = await CreateTeamAsync(client); var now = DateTimeOffset.UtcNow;
        var active = new AppointmentInvite(database.CompanyA, team, database.UnitA, TokenHash(), now.AddDays(7), database.ActorA, now);
        var expired = new AppointmentInvite(database.CompanyA, team, database.UnitA, TokenHash(), now.AddDays(7), database.ActorA, now);
        expired.Revoke(database.ActorA, expired.Version, now);
        await using (var db = database.CreateContext()) { db.AddRange(active, expired); await db.SaveChangesAsync(); }

        using var legacy = await client.DeleteAsync($"/api/v1/teams/{team}"); Assert.Equal(HttpStatusCode.BadRequest, legacy.StatusCode);
        using var impact = await client.GetAsync($"/api/v1/teams/{team}/deactivation-impact"); Assert.Equal(HttpStatusCode.OK, impact.StatusCode);
        var impactBody = await impact.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(impactBody.GetProperty("canDeactivate").GetBoolean()); Assert.Equal(1, impactBody.GetProperty("activeInviteCount").GetInt32());
        using var noReason = await Deactivate(client, team, 1, " "); Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        using var stale = await Deactivate(client, team, 9); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var done = await Deactivate(client, team, 1); Assert.True(done.StatusCode == HttpStatusCode.OK, await done.Content.ReadAsStringAsync());
        using var again = await Deactivate(client, team, 2); Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);

        await using var check = database.CreateContext();
        var stored = await check.Teams.IgnoreQueryFilters().SingleAsync(t => t.Id == team);
        Assert.False(stored.Active); Assert.NotNull(stored.DeletedAt); Assert.NotNull(stored.DeletedBy); Assert.Equal(2, stored.Version);
        Assert.NotNull((await check.Set<AppointmentInvite>().SingleAsync(i => i.Id == active.Id)).RevokedAt);
        var log = await check.AuditLogs.SingleAsync(a => a.ResourceId == team && a.Action == "TEAM_DEACTIVATED");
        Assert.True(log.IsIntegrityValid()); Assert.Equal(database.CompanyA, log.CompanyId); Assert.Contains("Equipe reorganizada", log.NewData);
    }

    [PostgresFact]
    public async Task FutureAppointmentsAndOpenPermitsBlockWithoutLeakingTheirTitle()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory, "ler", "criar", "excluir");
        var team = await CreateTeamAsync(client); var now = DateTimeOffset.UtcNow;
        var future = Appointment(team, now.AddDays(2));
        var cancelled = Appointment(team, now.AddDays(3)); cancelled.Cancel("Cancelado em teste", database.ActorA, cancelled.Version);
        var past = Appointment(team, now.AddDays(-3));
        await using (var db = database.CreateContext())
        {
            db.AddRange(future, cancelled, past);
            db.Add(new WorkPermit(database.CompanyA, past.Id, "MANUTENCAO", "PT em aberto de QA", database.ActorA));
            await db.SaveChangesAsync();
        }

        using var blocked = await Deactivate(client, team, 1); Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var text = await blocked.Content.ReadAsStringAsync(); var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("TEAM_HAS_PENDING_APPOINTMENTS", body.GetProperty("code").GetString());
        var impact = body.GetProperty("impact"); Assert.False(impact.GetProperty("canDeactivate").GetBoolean());
        Assert.Equal(2, impact.GetProperty("blockingAppointmentCount").GetInt32());
        Assert.Equal([past.Id, future.Id], impact.GetProperty("blockingAppointments").EnumerateArray().Select(a => a.GetProperty("appointmentId").GetGuid()));
        Assert.Equal("EM_VERIFICACAO", impact.GetProperty("blockingAppointments")[0].GetProperty("workPermitStatus").GetString());
        Assert.DoesNotContain("confidencial", text); Assert.DoesNotContain("PT em aberto", text);

        await using var check = database.CreateContext();
        var stored = await check.Teams.IgnoreQueryFilters().SingleAsync(t => t.Id == team);
        Assert.True(stored.Active); Assert.Equal(1, stored.Version);
        Assert.False(await check.AuditLogs.AnyAsync(a => a.ResourceId == team && a.Action == "TEAM_DEACTIVATED"));
    }

    [PostgresFact]
    public async Task ImpactAndDeactivationRequireTheExcluirPermission()
    {
        await using var factory = new PostgresApiFactory(database); using var writer = await ClientAsync(factory, "ler", "criar");
        var team = await CreateTeamAsync(writer);
        using var impact = await writer.GetAsync($"/api/v1/teams/{team}/deactivation-impact"); Assert.Equal(HttpStatusCode.Forbidden, impact.StatusCode);
        using var deactivate = await Deactivate(writer, team, 1); Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        await using var check = database.CreateContext(); Assert.True((await check.Teams.SingleAsync(t => t.Id == team)).Active);
    }
}
