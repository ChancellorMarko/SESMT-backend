using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class AppointmentPostgresTests(PostgresDatabaseFixture database)
{
    private static Guid[] CombinedServices() => WorkPermitServiceCatalogEntry.All
        .Where(service => service.Code is "QUENTE" or "CONFINADO").Select(service => service.Id).ToArray();

    [PostgresFact]
    public async Task Requests_ShouldRejectAnotherCompanyOrParticipantWithoutCreatingRecords()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        await using var before = database.CreateContext();
        var count = await before.WorkPermitAppointments.CountAsync();
        var starts = DateTimeOffset.UtcNow.AddDays(2);
        using var outside = await client.PostAsJsonAsync("/appointments", new
        {
            companyId = database.CompanyB, locationId = database.UnitB, teamId = database.TeamB,
            title = "Outra empresa", startsAt = starts, endsAt = starts.AddHours(1),
            serviceIds = CombinedServices(), participantIds = new[] { database.WorkerB },
        });
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        using var wrongMember = await client.PostAsJsonAsync("/appointments", new
        {
            companyId = database.CompanyA, locationId = database.UnitA, teamId = database.TeamA,
            title = "Participante de fora", startsAt = starts, endsAt = starts.AddHours(1),
            serviceIds = CombinedServices(), participantIds = new[] { database.WorkerB },
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongMember.StatusCode);
        await using var after = database.CreateContext();
        Assert.Equal(count, await after.WorkPermitAppointments.CountAsync());
    }

    [PostgresFact]
    public async Task Confirmations_ShouldReserveCapacityOnceUnderConcurrentHttpRequests()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var starts = DateTimeOffset.UtcNow.AddDays(1);
        async Task<Guid> RequestAsync(string title)
        {
            using var response = await client.PostAsJsonAsync("/appointments", new
            {
                companyId = database.CompanyA, locationId = database.UnitA, teamId = database.TeamA,
                title, startsAt = starts, endsAt = starts.AddHours(1), serviceIds = CombinedServices(),
                participantIds = new[] { database.WorkerA },
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(new[] { "CONFINADO", "QUENTE" }, payload.RootElement.GetProperty("serviceCodes")
                .EnumerateArray().Select(value => value.GetString()).Order());
            return payload.RootElement.GetProperty("id").GetGuid();
        }
        var first = await RequestAsync("Reserva fictícia A");
        var second = await RequestAsync("Reserva fictícia B");
        var responses = await Task.WhenAll(new[] { first, second }.Select(id =>
            client.PostAsJsonAsync($"/appointments/{id}/confirm", new { expectedVersion = 1, responsibleId = database.ActorA })));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using var verify = database.CreateContext();
            var appointments = await verify.WorkPermitAppointments.Where(item => item.Id == first || item.Id == second).ToArrayAsync();
            var winner = Assert.Single(appointments, item => item.Status == WorkPermitAppointment.Confirmed);
            var waiting = Assert.Single(appointments, item => item.Status == WorkPermitAppointment.Requested);
            Assert.Equal(1, waiting.Version);
            Assert.DoesNotContain(waiting.ReadHistory(), item => item.Action == "CONFIRMED");
            Assert.False(await verify.WorkPermits.AnyAsync(item => item.AppointmentId == first || item.AppointmentId == second));
            using var repeated = await client.PostAsJsonAsync($"/appointments/{winner.Id}/confirm",
                new { expectedVersion = 1, responsibleId = database.ActorA });
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            await verify.Entry(winner).ReloadAsync();
            Assert.Equal(2, winner.Version);
            Assert.Single(winner.ReadHistory(), item => item.Action == "CONFIRMED");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task Invite_ShouldDeriveItsScopeAndRejectOtherInvitesAndRevokedAccess()
    {
        await using var factory = new PostgresApiFactory(database);
        using var manager = factory.CreateClient();
        manager.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        async Task<(Guid Id, string Token)> CreateInviteAsync()
        {
            using var response = await manager.PostAsJsonAsync("/invites", new
            {
                companyId = database.CompanyA, teamId = database.TeamA, locationId = database.UnitA,
                expiresAt = DateTimeOffset.UtcNow.AddHours(4),
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var token = payload.RootElement.GetProperty("inviteUrl").GetString()!.Split('/').Last();
            Assert.True(token.Length == 43);
            return (payload.RootElement.GetProperty("id").GetGuid(), token);
        }
        var invite = await CreateInviteAsync();
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Invite-Token", invite.Token);
        using var context = await anonymous.GetAsync("/invites/context");
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        using (var payload = JsonDocument.Parse(await context.Content.ReadAsStringAsync()))
        {
            Assert.Equal(database.CompanyA, payload.RootElement.GetProperty("companyId").GetGuid());
            Assert.Equal(database.TeamA, payload.RootElement.GetProperty("teamId").GetGuid());
        }
        var starts = DateTimeOffset.UtcNow.AddDays(3);
        using var wrongMember = await anonymous.PostAsJsonAsync("/invites/appointments", new
        {
            title = "Participante incompatível", startsAt = starts, endsAt = starts.AddHours(1), serviceIds = CombinedServices(),
            participantIds = new[] { database.WorkerB },
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongMember.StatusCode);
        // These extra IDs are client-controlled and must never expand the invite's authority.
        using var created = await anonymous.PostAsJsonAsync("/invites/appointments", new
        {
            companyId = database.CompanyB, locationId = database.UnitB, teamId = database.TeamB,
            title = "Solicitação fictícia", startsAt = starts, endsAt = starts.AddHours(1), serviceIds = CombinedServices(),
            participantIds = new[] { database.WorkerA },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid requestId;
        using (var payload = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            var item = payload.RootElement;
            requestId = item.GetProperty("id").GetGuid();
            Assert.Equal(database.CompanyA, item.GetProperty("companyId").GetGuid());
            Assert.Equal(database.UnitA, item.GetProperty("locationId").GetGuid());
            Assert.Equal(database.TeamA, item.GetProperty("teamId").GetGuid());
            Assert.Equal(WorkPermitAppointment.Requested, item.GetProperty("status").GetString());
            Assert.Equal(invite.Id, item.GetProperty("requestedViaInviteId").GetGuid());
        }
        var otherInvite = await CreateInviteAsync();
        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-Invite-Token", otherInvite.Token);
        using var outside = await other.GetAsync($"/invites/appointments/{requestId}");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        using var dashboard = await anonymous.GetAsync("/work-permits");
        Assert.Equal(HttpStatusCode.Unauthorized, dashboard.StatusCode);
        using var revoke = await manager.PostAsJsonAsync($"/invites/{invite.Id}/revoke", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var revokedContext = await anonymous.GetAsync("/invites/context");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedContext.StatusCode);
        using var revokedRequest = await anonymous.PostAsJsonAsync("/invites/appointments", new
        {
            title = "Recusada após revogar", startsAt = starts, endsAt = starts.AddHours(1), serviceIds = CombinedServices(),
            participantIds = new[] { database.WorkerA },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRequest.StatusCode);
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.WorkPermitAppointments.CountAsync(item => item.RequestedViaInviteId == invite.Id));
    }

    [PostgresFact]
    public async Task Invite_ShouldRejectExpiryAndCrossCompanyCreation()
    {
        await using var factory = new PostgresApiFactory(database);
        using var manager = factory.CreateClient();
        manager.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var wrongCompany = await manager.PostAsJsonAsync("/invites", new
        {
            companyId = database.CompanyB, teamId = database.TeamB, locationId = database.UnitB,
            expiresAt = DateTimeOffset.UtcNow.AddHours(4),
        });
        Assert.Equal(HttpStatusCode.NotFound, wrongCompany.StatusCode);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow;
        var expired = new AppointmentInvite(database.CompanyA, database.TeamA, database.UnitA,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), now.AddDays(-1), database.ActorA, now.AddDays(-2));
        await using (var setup = database.CreateContext())
        {
            setup.Add(expired);
            await setup.SaveChangesAsync();
        }
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Invite-Token", token);
        using var response = await anonymous.GetAsync("/invites/context");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var read = await anonymous.GetAsync($"/invites/appointments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }
}
