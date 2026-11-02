using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class WorkerPermitPrivacyPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task WorkerSearchCountsAndDetailNeverIncludeAnotherPersonsPermit()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        await using var db = database.CreateContext();
        var person = new User("Pessoa fictícia com acesso próprio", Guid.NewGuid().ToString("N") + "@example.invalid", "fixture-only-hash");
        var role = await db.Roles.SingleAsync(r => r.Code == "TRABALHADOR" && r.Active && r.DeletedAt == null);
        var scope = new UserScope(person.Id, "EMPRESA"); scope.Update(person.Id, "EMPRESA", database.CompanyA);
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var appointment = new WorkPermitAppointment(database.CompanyA, "Atividade própria fictícia", start, start.AddHours(2), database.ActorA, database.UnitA);
        var permit = new WorkPermit(database.CompanyA, appointment.Id, "ALTURA", "PT própria fictícia", database.ActorA);
        permit.AddParticipant(new(permit.Id, person.Id, "Participante", database.ActorA), database.ActorA);
        db.AddRange(person, scope, new UserRole(person.Id, role.Id, database.CompanyA), appointment, permit); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Add("X-Test-Actor", person.Id.ToString());
        using var search = await client.GetAsync("/api/v1/work-permits/search?page=1&pageSize=1"); Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var results = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Equal(1, results.RootElement.GetProperty("total").GetInt32()); Assert.Equal(permit.Id, results.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var owned = await client.GetAsync($"/api/v1/work-permits/{permit.Id}"); Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        foreach (var path in new[] { $"/api/v1/work-permits/{database.PermitA}", $"/api/v1/work-permits/{database.PermitA}/history", $"/api/v1/work-permits/{database.PermitA}/eligibility" })
        { using var forbidden = await client.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode); }
        using var tampered = await client.GetAsync($"/api/v1/work-permits/search?participantUserId={database.ActorB}");
        using var filtered = JsonDocument.Parse(await tampered.Content.ReadAsStringAsync()); Assert.Equal(0, filtered.RootElement.GetProperty("total").GetInt32());
    }
}
