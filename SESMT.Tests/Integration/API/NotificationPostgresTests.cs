using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class NotificationPostgresTests(PostgresDatabaseFixture database)
{
    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private HttpClient AuthClient(PostgresApiFactory factory, Guid? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (actor ?? database.ActorA).ToString());
        return client;
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Insere uma notificação diretamente via DbContext (o sistema não tem
    /// endpoint público para criar alertas — eles nascem em background jobs).
    /// </summary>
    private async Task<Guid> SeedAlertAsync(Guid destinationUserId)
    {
        await using var db = database.CreateContext();

        var documentId = destinationUserId == database.ActorA ? database.DocumentA : database.DocumentB;
        var alert = new Alert("VENCIMENTO_DOC", "DOCUMENT", documentId, Guid.NewGuid(),
            destinationUserId, "IN_APP", "Documento vencendo", "Evidência fictícia vence em 7 dias.", DateTimeOffset.UtcNow);
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET /notifications
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync("/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task List_ReturnsOnlyOwnNotifications()
    {
        await using var factory = new PostgresApiFactory(database);

        var ownAlertId = await SeedAlertAsync(database.ActorA);
        var otherAlertId = await SeedAlertAsync(database.ActorB);

        using var client = AuthClient(factory, database.ActorA);
        using var response = await client.GetAsync("/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        var items = body.GetProperty("items").EnumerateArray().ToArray();

        // Other tests can have created legitimate alerts for the same actor.
        Assert.Contains(items, item => item.GetProperty("id").GetGuid() == ownAlertId);
        await using var stored = database.CreateContext();
        var returnedIds = items.Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(returnedIds.Length, await stored.Alerts.CountAsync(a => returnedIds.Contains(a.Id) && a.DestinationUserId == database.ActorA));

        // A do ActorB não pode aparecer
        Assert.DoesNotContain(items, item =>
            item.GetProperty("id").GetGuid() == otherAlertId);
    }

    [PostgresFact]
    public async Task List_WithoutNotifications_ReturnsEmptyItems()
    {
        await using var factory = new PostgresApiFactory(database);

        // A fresh, scoped account has no notifications, regardless of test order.
        await using var setup = database.CreateContext();
        var user = new User("Pessoa sem notificações", $"no-alert-{Guid.NewGuid():N}@example.invalid", "not-a-login-hash");
        var scope = new UserScope(user.Id, "EMPRESA");
        scope.Update(user.Id, "EMPRESA", database.CompanyA);
        setup.AddRange(user, scope);
        await setup.SaveChangesAsync();
        using var client = AuthClient(factory, user.Id);
        using var response = await client.GetAsync("/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    // ════════════════════════════════════════════════════════════════════════
    // POST /notifications/{id}/read
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task MarkRead_WithOwnNotification_Returns204AndPersistsReadAt()
    {
        await using var factory = new PostgresApiFactory(database);
        var alertId = await SeedAlertAsync(database.ActorA);

        using var client = AuthClient(factory, database.ActorA);
        using var response = await client.PostAsync($"/notifications/{alertId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verifica persistência no banco
        await using var db = database.CreateContext();
        var alert = await db.Alerts.SingleAsync(a => a.Id == alertId);
        Assert.NotNull(alert.ReadAt);
        Assert.Equal("LIDA", alert.Status);
    }

    [PostgresFact]
    public async Task MarkRead_WithUnknownId_Returns404()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.PostAsync(
            $"/notifications/{Guid.NewGuid()}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("NOTIFICATION_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task MarkRead_WithSomeoneElsesNotification_Returns404AndDoesNotChangeIt()
    {
        await using var factory = new PostgresApiFactory(database);

        // Alerta pertence ao ActorB
        var alertId = await SeedAlertAsync(database.ActorB);

        // ActorA tenta marcar como lido → isolamento
        using var client = AuthClient(factory, database.ActorA);
        using var response = await client.PostAsync($"/notifications/{alertId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Alerta continua intocado
        await using var db = database.CreateContext();
        var alert = await db.Alerts.SingleAsync(a => a.Id == alertId);
        Assert.Null(alert.ReadAt);
        Assert.Equal("NOVA", alert.Status);
    }

    [PostgresFact]
    public async Task MarkRead_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        var alertId = await SeedAlertAsync(database.ActorA);

        using var client = factory.CreateClient(); // sem X-Test-Actor
        using var response = await client.PostAsync($"/notifications/{alertId}/read", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
