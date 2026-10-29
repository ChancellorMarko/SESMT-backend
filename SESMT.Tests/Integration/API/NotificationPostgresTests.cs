using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Notifications.Entities;

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

        var id = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO core.alerta (
            id, tipo, entidade_tipo, entidade_id, severidade,
            destinatario_id, canal, titulo, mensagem,
            data_disparo, data_leitura, status, escalonamento_nivel,
            ativo, criado_em, criado_por,
            atualizado_em, atualizado_por, excluido_em, excluido_por
        ) VALUES (
            {id}, 'VENCIMENTO_DOC', 'documento', {Guid.NewGuid()}, 'INFO',
            {destinationUserId}, 'SISTEMA', 'Documento vencendo', 'O documento fictício vence em 7 dias.',
            {DateTimeOffset.UtcNow}, {null}, 'PENDENTE', 0,
            true, {DateTimeOffset.UtcNow}, {null},
            {null}, {null}, {null}, {null}
        )
        """);

        return id;
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

        // Só a notificação do ActorA deve aparecer
        Assert.Single(items);
        Assert.Equal(ownAlertId, items[0].GetProperty("id").GetGuid());

        // A do ActorB não pode aparecer
        Assert.DoesNotContain(items, item =>
            item.GetProperty("id").GetGuid() == otherAlertId);
    }

    [PostgresFact]
    public async Task List_WithoutNotifications_ReturnsEmptyItems()
    {
        await using var factory = new PostgresApiFactory(database);

        // Nenhum seed de alert → lista vazia
        using var client = AuthClient(factory, database.ActorA);
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
        Assert.Equal("LIDO", alert.Status);
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
        Assert.Equal("PENDENTE", alert.Status);
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
