using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class PasswordResetPostgresTests(PostgresDatabaseFixture database)
{
    private const string OriginalPassword = "OriginalPassword-2026!";

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cria um usuário ativo isolado no banco (não usa o seed admin,
    /// para não impactar outros testes).
    /// </summary>
    private static async Task<(Guid UserId, string Email)> SeedUserAsync(
        PostgresApiFactory factory,
        PostgresDatabaseFixture database)
    {
        var passwordHasher = factory.Services.GetRequiredService<IPasswordHashService>();
        var hash = passwordHasher.HashPassword(OriginalPassword);
        var email = $"reset-test-{Guid.NewGuid():N}@example.invalid";
        var user = new User("Password Reset Test User", email, hash);

        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, email);
    }

    /// <summary>
    /// Semeia um PasswordResetToken válido (não usado, não expirado)
    /// e devolve o token em texto plano.
    /// </summary>
    private static async Task<string> SeedResetTokenAsync(
        PostgresApiFactory factory,
        PostgresDatabaseFixture database,
        Guid userId)
    {
        var refreshTokenService = factory.Services.GetRequiredService<IRefreshTokenService>();

        var rawToken = "reset-" + Guid.NewGuid().ToString("N");
        var tokenHash = refreshTokenService.HashToken(rawToken);

        await using var db = database.CreateContext();
        var token = new PasswordResetToken(
            userId,
            tokenHash,
            DateTimeOffset.UtcNow.AddMinutes(30));

        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();

        return rawToken;
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Forgot password (anti-enumeration)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ForgotPassword_KnownEmail_Returns202WithGenericMessage()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (_, email) = await SeedUserAsync(factory, database);

        using var response = await client.PostAsJsonAsync(
            "/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [PostgresFact]
    public async Task ForgotPassword_UnknownEmail_ReturnsSame202AsKnownEmail()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (_, knownEmail) = await SeedUserAsync(factory, database);
        var unknownEmail = $"unknown-{Guid.NewGuid():N}@example.invalid";

        using var knownResponse = await client.PostAsJsonAsync(
            "/auth/forgot-password", new { email = knownEmail });
        using var unknownResponse = await client.PostAsJsonAsync(
            "/auth/forgot-password", new { email = unknownEmail });

        // ⭐ Anti-enumeration: mesma resposta para existente e inexistente
        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        var knownBody = await PayloadAsync(knownResponse);
        var unknownBody = await PayloadAsync(unknownResponse);

        Assert.Equal(
            knownBody.GetProperty("message").GetString(),
            unknownBody.GetProperty("message").GetString());
    }

    // ════════════════════════════════════════════════════════════════════════
    // Reset password — casos de erro
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ResetPassword_WithInvalidToken_Returns400InvalidResetToken()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = "this-token-does-not-exist",
            newPassword = "NovaSenha-2026!Segura"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_RESET_TOKEN", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task ResetPassword_WithShortPassword_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (userId, _) = await SeedUserAsync(factory, database);
        var rawToken = await SeedResetTokenAsync(factory, database, userId);

        // newPassword com menos de 12 caracteres → rejeitado pelo service
        using var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "curta"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_RESET_TOKEN", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task ResetPassword_WithEmptyBody_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/reset-password", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Reset password — caso de sucesso completo
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndAllowsNewLogin()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (userId, email) = await SeedUserAsync(factory, database);
        var rawToken = await SeedResetTokenAsync(factory, database, userId);

        var newPassword = "NovaSenha-2026!Segura";

        // 1. Reset com token válido → 204 NoContent
        using (var reset = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword
        }))
        {
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        }

        // 2. Token deve estar marcado como usado no banco
        await using (var db = database.CreateContext())
        {
            var refreshTokenService = factory.Services.GetRequiredService<IRefreshTokenService>();
            var expectedHash = refreshTokenService.HashToken(rawToken);

            var token = await db.PasswordResetTokens
                .SingleAsync(t => t.UserId == userId && t.TokenHash == expectedHash);

            Assert.NotNull(token.UsedAt);
        }

        // 3. Login com a nova senha funciona
        using (var newLogin = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = newPassword
        }))
        {
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        }

        // 4. Login com a senha antiga falha
        using (var oldLogin = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = OriginalPassword
        }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Token não pode ser reutilizado
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ResetPassword_SameTokenTwice_SecondFailsWith400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (userId, _) = await SeedUserAsync(factory, database);
        var rawToken = await SeedResetTokenAsync(factory, database, userId);

        // Primeiro reset → OK
        using (var first = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "PrimeiraSenha-2026!"
        }))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        // Segundo reset com o mesmo token → falha (já consumido)
        using var second = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "SegundaSenha-2026!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var body = await PayloadAsync(second);
        Assert.Equal("INVALID_RESET_TOKEN", body.GetProperty("code").GetString());
    }

    // ════════════════════════════════════════════════════════════════════════
    // Token expirado
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ResetPassword_WithExpiredToken_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();

        var (userId, _) = await SeedUserAsync(factory, database);

        // Semeia token já expirado
        var refreshTokenService = factory.Services.GetRequiredService<IRefreshTokenService>();
        var rawToken = "expired-" + Guid.NewGuid().ToString("N");
        var hash = refreshTokenService.HashToken(rawToken);

        await using (var db = database.CreateContext())
        {
            db.PasswordResetTokens.Add(new PasswordResetToken(
                userId,
                hash,
                DateTimeOffset.UtcNow.AddMinutes(-5))); // ← expirado
            await db.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "NovaSenha-2026!Segura"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_RESET_TOKEN", body.GetProperty("code").GetString());
    }
}
