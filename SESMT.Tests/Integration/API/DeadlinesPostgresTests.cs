using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Equipment.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.OccupationalHealth.Entities;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DeadlinesPostgresTests(PostgresDatabaseFixture database)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        await using var db = database.CreateContext(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString());
        return client;
    }

    private async Task<HttpClient> Reader(PostgresApiFactory factory, params (string Resource, string Action)[] permissions)
    {
        using var warmup = factory.CreateClient();
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var user = new User("Leitura de prazos", key + "@example.invalid", "fixture-only-hash");
        var role = new Role("QA_DEADLINE_" + key[..20], "Consulta de prazos", "Consulta isolada de teste");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA));
        foreach (var (resource, action) in permissions)
            db.Add(new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == resource && p.Action == action)).Id));
        await db.SaveChangesAsync();
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString()); return client;
    }

    private async Task<Worker> NewWorker(Guid? user = null)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var worker = new Worker(database.CompanyA, "DL-" + key[..20], [1, 2, 3], key + key, "Pessoa com prazo", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, user);
        db.Add(worker); await db.SaveChangesAsync(); return worker;
    }

    private static TrainingRecord Training(PostgresDatabaseFixture f, Guid worker, string title, DateOnly completed, DateOnly? renewal)
    {
        var t = new TrainingRecord(f.CompanyA, f.UnitA, worker, Guid.NewGuid(), f.ActorA);
        t.Set(title, null, "PERIODIC", completed, renewal, 60, null, null, null, null, null); return t;
    }

    private static OccupationalCertificate Aso(PostgresDatabaseFixture f, Guid worker, DateOnly exam, DateOnly? next)
    {
        var a = new OccupationalCertificate(f.CompanyA, f.UnitA, worker, Guid.NewGuid(), f.ActorA);
        a.Set("PERIODIC", exam, exam, next, "protected-fixture"); return a;
    }

    private static async Task<JsonElement> Get(HttpClient client, string query, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.GetAsync("/api/v1/deadlines" + query);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
        return expected == HttpStatusCode.OK ? JsonDocument.Parse(text).RootElement.Clone() : default;
    }

    [PostgresFact]
    public async Task ListsOnlyCurrentOverdueAndDueSoonItemsWithoutClinicalData()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await Reader(factory, ("treinamento", "ler"), ("aso", "acompanhar"), ("epi", "ler"));
        using var admin = await Admin(factory);
        var worker = await NewWorker();
        await using (var db = database.CreateContext())
        {
            var equipment = new ProtectiveEquipment(database.CompanyA, "DL" + Guid.NewGuid().ToString("N")[..10], "Luva de prazo", "12345", null, null, database.ActorA);
            var otherEquipment = new ProtectiveEquipment(database.CompanyA, "DL" + Guid.NewGuid().ToString("N")[..10], "Óculos de prazo", null, null, null, database.ActorA);
            var returned = new EquipmentDelivery(otherEquipment, database.UnitA, worker.Id, Guid.NewGuid(), 1, null, Today.AddDays(-90), Today.AddDays(-1), null, database.ActorA);
            returned.Return(Today.AddDays(-2), "Devolvido ao estoque", database.ActorA);
            var revoked = Training(database, worker.Id, "NR-35 revogado", Today.AddDays(-400), Today.AddDays(-5));
            revoked.Revoke("Registro duplicado", database.ActorA, DateTimeOffset.UtcNow);
            db.AddRange(equipment, otherEquipment, returned, revoked,
                Training(database, worker.Id, "NR-10 básico", Today.AddDays(-400), Today.AddDays(-10)),
                Training(database, worker.Id, "NR-33 vigia", Today.AddDays(-700), Today.AddDays(-300)),
                Training(database, worker.Id, "nr-33 VIGIA", Today.AddDays(-30), Today.AddDays(335)),
                Training(database, worker.Id, "Brigada", Today.AddDays(-10), Today.AddDays(200)),
                Aso(database, worker.Id, Today.AddDays(-700), Today.AddDays(-335)),
                Aso(database, worker.Id, Today.AddDays(-350), Today.AddDays(15)),
                new EquipmentDelivery(equipment, database.UnitA, worker.Id, Guid.NewGuid(), 2, "M", Today.AddDays(-20), Today.AddDays(3), null, database.ActorA));
            await db.SaveChangesAsync();
        }

        var body = await Get(client, $"?workerId={worker.Id}");
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["TRAINING", "EPI", "ASO"], items.Select(i => i.GetProperty("kind").GetString()));
        Assert.Equal(["NR-10 básico", "Luva de prazo", "Próxima avaliação do ASO"], items.Select(i => i.GetProperty("label").GetString()));
        Assert.Equal(["OVERDUE", "DUE_SOON", "DUE_SOON"], items.Select(i => i.GetProperty("status").GetString()));
        Assert.Equal(1, body.GetProperty("summary").GetProperty("overdue").GetInt32());
        Assert.Equal(2, body.GetProperty("summary").GetProperty("dueSoon").GetInt32());
        Assert.All(items, i => Assert.Equal(worker.Id, i.GetProperty("workerId").GetGuid()));
        var raw = body.GetRawText();
        Assert.DoesNotContain("PERIODIC", raw); Assert.DoesNotContain("protected-fixture", raw); Assert.DoesNotContain("12345", raw);

        var adminView = await Get(admin, $"?workerId={worker.Id}");
        Assert.DoesNotContain("ASO", adminView.GetProperty("kinds").EnumerateArray().Select(k => k.GetString()));
        Assert.DoesNotContain(adminView.GetProperty("items").EnumerateArray(), i => i.GetProperty("kind").GetString() == "ASO");

        var wider = await Get(client, $"?workerId={worker.Id}&days=365&kind=TRAINING");
        Assert.Equal(["NR-10 básico", "Brigada", "nr-33 VIGIA"], wider.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("label").GetString()));
    }

    [PostgresFact]
    public async Task OwnOnlyAccountSeesOnlyItsOwnDeadlines()
    {
        await using var factory = new PostgresApiFactory(database); using var warmup = factory.CreateClient(); await using var db = database.CreateContext();
        var key = Guid.NewGuid().ToString("N"); var user = new User("Pessoa própria", key + "@example.invalid", "fixture-only-hash");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, scope, new UserRole(user.Id, (await db.Roles.SingleAsync(r => r.Code == "TRABALHADOR")).Id, database.CompanyA));
        await db.SaveChangesAsync();
        var mine = await NewWorker(user.Id); var other = await NewWorker();
        db.AddRange(Training(database, mine.Id, "Meu curso", Today.AddDays(-30), Today.AddDays(5)),
            Training(database, other.Id, "Curso alheio", Today.AddDays(-30), Today.AddDays(5)));
        await db.SaveChangesAsync();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString());

        var body = await Get(client, "?days=365");
        var workers = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("workerId").GetGuid()).Distinct().ToArray();
        Assert.Equal([mine.Id], workers);
        var foreign = await Get(client, $"?workerId={other.Id}");
        Assert.Equal(0, foreign.GetProperty("total").GetInt32());
    }

    [PostgresFact]
    public async Task EachSourceRequiresItsOwnReadPermissionAndScope()
    {
        await using var factory = new PostgresApiFactory(database); using var warmup = factory.CreateClient(); await using var db = database.CreateContext();
        var key = Guid.NewGuid().ToString("N"); var user = new User("Leitura de cursos", key + "@example.invalid", "fixture-only-hash");
        var role = new Role("QA_DEADLINE_" + key[..20], "Somente cursos", "Consulta isolada de teste");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA),
            new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "treinamento" && p.Action == "ler")).Id));
        await db.SaveChangesAsync();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString());

        var body = await Get(client, "");
        Assert.Equal(["TRAINING"], body.GetProperty("kinds").EnumerateArray().Select(k => k.GetString()));
        Assert.All(body.GetProperty("items").EnumerateArray(), i => Assert.Equal("TRAINING", i.GetProperty("kind").GetString()));
        await Get(client, "?kind=ASO", HttpStatusCode.Forbidden);
        await Get(client, $"?unitId={database.UnitB}", HttpStatusCode.Forbidden);
        await Get(client, "?days=0", HttpStatusCode.BadRequest);
        await Get(client, "?kind=OTHER", HttpStatusCode.BadRequest);
        using var anonymous = factory.CreateClient();
        using var noSession = await anonymous.GetAsync("/api/v1/deadlines"); Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
    }
}
