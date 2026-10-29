using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.WorkPermits.Constants;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class PostgresApiIntegrationTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task Migrations_ShouldCreateSchemaFromAnEmptyIsolatedDatabase()
    {
        await using var db = database.CreateContext();
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Companies.AnyAsync(company => company.Id == database.CompanyA));
        Assert.True(await db.WorkPermitTransitions.AnyAsync(item => item.WorkPermitId == database.PermitA));
    }

    [PostgresFact]
    public async Task Api_ShouldRejectAnonymousRequests()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/work-permits");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Api_ShouldScopeTheListAndHideAnotherCompanysDirectUrl()
    {
        await using var db = database.CreateContext();
        var expected = await db.WorkPermits.Where(permit => permit.CompanyId == database.CompanyA && permit.DeletedAt == null)
            .Select(permit => permit.Id).ToArrayAsync();
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var response = await client.GetAsync("/work-permits");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("items");
        var ids = items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(expected.Order(), ids.Order());
        if (root.ValueKind == JsonValueKind.Object) Assert.Equal(expected.Length, root.GetProperty("total").GetInt32());
        Assert.DoesNotContain(database.PermitB, ids);
        using var outside = await client.GetAsync($"/work-permits/{database.PermitB}");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
    }

    [PostgresFact]
    public async Task Api_SearchShouldCombineFiltersAndCountOnlyTheAuthorizedCompany()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var filters = $"search={Uri.EscapeDataString("PT fictícia")}&serviceCode=MANUTENCAO&status={WorkPermitStatus.UnderReview}&pageSize=1";
        using var response = await client.GetAsync($"/work-permits/search?{filters}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var page = payload.RootElement;
            Assert.Equal(1, page.GetProperty("total").GetInt32());
            var item = Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.Equal(database.PermitA, item.GetProperty("id").GetGuid());
            Assert.Equal(database.CompanyA, item.GetProperty("companyId").GetGuid());
        }
        using var secondPage = await client.GetAsync($"/pts?{filters}&page=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        using (var payload = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, payload.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(payload.RootElement.GetProperty("items").EnumerateArray());
        }
        using var forgedScope = await client.GetAsync($"/work-permits/search?companyId={database.CompanyB}&{filters}");
        Assert.Equal(HttpStatusCode.NotFound, forgedScope.StatusCode);
    }

    [PostgresFact]
    public async Task Api_ShouldRefuseReleaseWithoutDesignationAndPreserveStateAndHistory()
    {
        await using var before = database.CreateContext();
        var permit = await before.WorkPermits.SingleAsync(item => item.Id == database.PermitA);
        var appointment = await before.WorkPermitAppointments.SingleAsync(item => item.Id == permit.AppointmentId);
        var historyCount = await before.WorkPermitTransitions.CountAsync(item => item.WorkPermitId == permit.Id);
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var response = await client.PostAsJsonAsync($"/work-permits/{permit.Id}/release", new
        {
            expectedVersion = permit.Version,
            conditions = "Exemplo de teste isolado; não autoriza operação real.",
            authorizedFrom = appointment.StartsAt,
            authorizedUntil = appointment.EndsAt,
            confirmation = true,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var after = database.CreateContext();
        var stored = await after.WorkPermits.SingleAsync(item => item.Id == permit.Id);
        Assert.Equal(permit.Version, stored.Version);
        Assert.Equal(permit.Status, stored.Status);
        Assert.Null(stored.ReleasedAt);
        Assert.Equal(historyCount, await after.WorkPermitTransitions.CountAsync(item => item.WorkPermitId == permit.Id));
    }

    [PostgresFact]
    public async Task Persistence_ShouldRejectAStaleVersionBetweenConcurrentContexts()
    {
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var original = await first.WorkPermits.SingleAsync(item => item.Id == database.PermitA);
        var stale = await second.WorkPermits.SingleAsync(item => item.Id == database.PermitA);
        var version = original.Version;
        original.SaveChecklist([], database.ActorA);
        stale.SaveChecklist([], database.ActorA);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = database.CreateContext();
        Assert.Equal(version + 1, (await check.WorkPermits.SingleAsync(item => item.Id == original.Id)).Version);
    }

    [PostgresFact]
    public async Task Persistence_ShouldAllowOnlyOnePermitPerAppointmentUnderConcurrency()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = new WorkPermitAppointment(database.CompanyA, "Concorrência fictícia", now.AddHours(3), now.AddHours(4), database.ActorA);
        await using (var setup = database.CreateContext())
        {
            setup.Add(appointment);
            await setup.SaveChangesAsync();
        }
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        first.Add(new WorkPermit(database.CompanyA, appointment.Id, "MANUTENCAO", "Primeira abertura", database.ActorA));
        second.Add(new WorkPermit(database.CompanyA, appointment.Id, "MANUTENCAO", "Abertura concorrente", database.ActorA));
        static async Task<DbUpdateException?> SaveAsync(SESMT.Infrastructure.Persistence.SesmtDbContext context)
        {
            try { await context.SaveChangesAsync(); return null; }
            catch (DbUpdateException exception) { return exception; }
        }
        var results = await Task.WhenAll(SaveAsync(first), SaveAsync(second));
        Assert.Single(results, result => result is null);
        var failure = Assert.Single(results, result => result is not null)!;
        var postgres = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_permissao_trabalho_agendamento", postgres.ConstraintName);
        await using var verify = database.CreateContext();
        var storedId = await verify.WorkPermits.Where(item => item.AppointmentId == appointment.Id).Select(item => item.Id).SingleAsync();
        Assert.Single(await verify.WorkPermitTransitions.Where(item => item.WorkPermitId == storedId).ToArrayAsync());
    }

    [PostgresFact]
    public async Task Persistence_ShouldKeepTransitionHistoryAppendOnly()
    {
        await using var db = database.CreateContext();
        var transition = await db.WorkPermitTransitions.FirstAsync(item => item.WorkPermitId == database.PermitB);
        db.Remove(transition);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var verify = database.CreateContext();
        Assert.True(await verify.WorkPermitTransitions.AnyAsync(item => item.Id == transition.Id));
    }
}
