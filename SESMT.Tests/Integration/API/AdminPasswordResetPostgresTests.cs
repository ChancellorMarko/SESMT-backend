using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

/// <summary>An administrator can lock an account out of its password and sessions, but never learns or sets the new password.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminPasswordResetPostgresTests(PostgresDatabaseFixture database)
{
    private const string Reason = "Celular perdido; acesso precisa ser refeito pela própria pessoa.";

    private async Task<string> AllowManagementAsync()
    {
        await using var db = database.CreateContext();
        var role = new Role("QA_RESET_" + Guid.NewGuid().ToString("N")[..24], "Gestão de contas fictícia", "Somente teste isolado");
        db.AddRange(role, new UserRole(database.ActorA, role.Id, database.CompanyA));
        foreach (var action in new[] { "ler", "criar", "editar" })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == "usuario" && p.Action == action && p.Active && p.DeletedAt == null);
            if (permission is null) { permission = new Permission("usuario", action, "Teste isolado"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync(); return role.Code;
    }

    private async Task<Guid> CreateTargetAsync(HttpClient client, string role)
    {
        using var reply = await client.PostAsJsonAsync("/api/v1/users", new { name = "Conta fictícia do teste", email = $"qa-{Guid.NewGuid():N}@example.invalid", roles = new[] { role }, scopeIds = new[] { database.CompanyA } });
        var text = await reply.Content.ReadAsStringAsync(); Assert.True(reply.StatusCode == HttpStatusCode.Created, text);
        using var parsed = JsonDocument.Parse(text); return parsed.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, PostgresApiFactory Factory, Guid Target)> ArrangeAsync()
    {
        var factory = new PostgresApiFactory(database); var client = factory.CreateClient();
        var role = await AllowManagementAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        return (client, factory, await CreateTargetAsync(client, role));
    }

    private static async Task<JsonDocument> ResetAsync(HttpClient client, Guid target)
    {
        using var reply = await client.PostAsJsonAsync($"/api/v1/users/{target}/password-reset", new { reason = Reason, confirmation = true });
        var text = await reply.Content.ReadAsStringAsync(); Assert.True(reply.StatusCode == HttpStatusCode.OK, text);
        Assert.Contains("no-store", reply.Headers.CacheControl?.ToString() ?? "");
        return JsonDocument.Parse(text);
    }

    [PostgresFact]
    public async Task EndsSessionsLocksTheOldPasswordAndOnlyTheHolderOfTheTokenDefinesTheNewOne()
    {
        var (client, factory, target) = await ArrangeAsync();
        await using var _ = factory; using var __ = client;
        string oldHash;
        await using (var db = database.CreateContext())
        {
            db.AddRange(new UserSession(target, "qa-session-a-" + Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)),
                new UserSession(target, "qa-session-b-" + Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)));
            await db.SaveChangesAsync();
            oldHash = (await db.Users.SingleAsync(u => u.Id == target)).PasswordHash;
        }

        using var body = await ResetAsync(client, target);
        var token = body.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(2, body.RootElement.GetProperty("sessionsEnded").GetInt32());
        var expiresAt = body.RootElement.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));

        await using (var db = database.CreateContext())
        {
            Assert.NotEqual(oldHash, (await db.Users.SingleAsync(u => u.Id == target)).PasswordHash);
            Assert.Equal(0, await db.UserSessions.CountAsync(s => s.UserId == target && s.RevokedAt == null));
            var log = await db.AuditLogs.SingleAsync(l => l.ResourceId == target && l.Action == "PASSWORD_RESET_ISSUED");
            Assert.True(log.IsIntegrityValid());
            Assert.Equal(database.ActorA, log.ActorId);
            var stored = JsonSerializer.Serialize(log);
            Assert.DoesNotContain(token, stored);
            Assert.Contains("Celular perdido", stored);
            Assert.DoesNotContain(token, await db.PasswordResetTokens.Where(t => t.UserId == target).Select(t => t.TokenHash).FirstAsync());
        }

        using var anonymous = factory.CreateClient();
        using var reset = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "Nova-senha-da-pessoa-2026" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var reused = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "Outra-senha-qualquer-2026" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [PostgresFact]
    public async Task ASecondResetCancelsTheFirstLink()
    {
        var (client, factory, target) = await ArrangeAsync();
        await using var _ = factory; using var __ = client;
        using var first = await ResetAsync(client, target);
        using var second = await ResetAsync(client, target);
        using var anonymous = factory.CreateClient();
        using var stale = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = first.RootElement.GetProperty("token").GetString(), newPassword = "Nova-senha-da-pessoa-2026" });
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        using var current = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = second.RootElement.GetProperty("token").GetString(), newPassword = "Nova-senha-da-pessoa-2026" });
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    [PostgresFact]
    public async Task RefusalsLeaveThePasswordSessionsAndAuditUntouched()
    {
        var (client, factory, target) = await ArrangeAsync();
        await using var _ = factory; using var __ = client;
        string hash;
        await using (var db = database.CreateContext())
        {
            db.Add(new UserSession(target, "qa-session-" + Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)));
            await db.SaveChangesAsync();
            hash = (await db.Users.SingleAsync(u => u.Id == target)).PasswordHash;
        }

        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/users/{target}/password-reset", new { reason = Reason, confirmation = false });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        using var noReason = await client.PostAsJsonAsync($"/api/v1/users/{target}/password-reset", new { reason = "curto", confirmation = true });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        using var own = await client.PostAsJsonAsync($"/api/v1/users/{database.ActorA}/password-reset", new { reason = Reason, confirmation = true });
        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
        // ActorB belongs to company B, outside this administrator's scope.
        using var foreign = await client.PostAsJsonAsync($"/api/v1/users/{database.ActorB}/password-reset", new { reason = Reason, confirmation = true });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var unknown = await client.PostAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/password-reset", new { reason = Reason, confirmation = true });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        await using var check = database.CreateContext();
        Assert.Equal(hash, (await check.Users.SingleAsync(u => u.Id == target)).PasswordHash);
        Assert.Equal(1, await check.UserSessions.CountAsync(s => s.UserId == target && s.RevokedAt == null));
        Assert.Equal(0, await check.PasswordResetTokens.CountAsync(t => t.UserId == target));
        Assert.Equal(0, await check.AuditLogs.CountAsync(l => l.Action == "PASSWORD_RESET_ISSUED" && (l.ResourceId == target || l.ResourceId == database.ActorB || l.ResourceId == database.ActorA)));
    }

    [PostgresFact]
    public async Task ADeactivatedAccountIsOutOfReachAndGetsNoLink()
    {
        var (client, factory, target) = await ArrangeAsync();
        await using var _ = factory; using var __ = client;
        await using (var db = database.CreateContext())
        {
            (await db.Users.SingleAsync(u => u.Id == target)).Deactivate(database.ActorA);
            await db.SaveChangesAsync();
        }
        // Deactivation is a logical deletion, so the account is not found, as in the other account commands.
        using var reply = await client.PostAsJsonAsync($"/api/v1/users/{target}/password-reset", new { reason = Reason, confirmation = true });
        Assert.Equal(HttpStatusCode.NotFound, reply.StatusCode);
        await using var check = database.CreateContext();
        Assert.Equal(0, await check.PasswordResetTokens.CountAsync(t => t.UserId == target));
        Assert.Equal(0, await check.AuditLogs.CountAsync(l => l.ResourceId == target && l.Action == "PASSWORD_RESET_ISSUED"));
    }
}
