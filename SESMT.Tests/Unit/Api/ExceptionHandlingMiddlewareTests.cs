using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SESMT.API.Middleware;
using SESMT.Application.Common.Interfaces;

namespace SESMT.Tests.Unit.Api;

public sealed class ExceptionHandlingMiddlewareTests
{
    private sealed class Anonymous : ICurrentUserService
    {
        public Guid? UserId => null; public string? Email => null; public string? Name => null;
        public string? IpAddress => null; public string? UserAgent => null; public string? TraceId => null; public bool IsAuthenticated => false;
    }

    /// <summary>Counts scope requests: the middleware opens one only to write the audit trail (and swallows its errors).</summary>
    private sealed class CountingScopes : IServiceScopeFactory
    {
        public int Calls;
        public IServiceScope CreateScope() { Calls++; throw new InvalidOperationException("Sem banco no teste unitário."); }
    }

    private static async Task<(int Status, JsonElement Body)> Run(Exception exception, int? presetStatus = null, CountingScopes? scopes = null)
    {
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ =>
        {
            if (presetStatus.HasValue) { context.Response.StatusCode = presetStatus.Value; return Task.CompletedTask; }
            throw exception;
        }, NullLogger<ExceptionHandlingMiddleware>.Instance, scopes ?? new CountingScopes());
        await middleware.InvokeAsync(context, new Anonymous());
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static DbUpdateException Persistence(string sqlState) =>
        new("An error occurred while saving the entity changes. See the inner exception for details.",
            new PostgresException("duplicate key value violates unique constraint \"ix_core_usuario_email\"", "ERROR", "ERROR", sqlState));

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "DUPLICATE", "Já existe um registro com esses dados.")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "REFERENCE_VIOLATION", "O registro faz referência a um item inexistente ou em uso.")]
    [InlineData(PostgresErrorCodes.CheckViolation, "CONFLICT", "Não foi possível gravar a alteração. Atualize e tente novamente.")]
    public async Task PersistenceErrorsNeverEchoProviderText(string sqlState, string code, string message)
    {
        var (status, body) = await Run(Persistence(sqlState));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(message, body.GetProperty("message").GetString());
        Assert.DoesNotContain("constraint", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inner exception", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidationMessagesDropTheInternalParameterName()
    {
        var (status, body) = await Run(new ArgumentException("Informe o motivo.", "reason"));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("Informe o motivo.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedFailuresKeepAGenericMessage()
    {
        var (status, body) = await Run(new InvalidOperationException("Connection string Host=db;Password=segredo"));
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("Ocorreu um erro inesperado.", body.GetProperty("message").GetString());
        Assert.DoesNotContain("segredo", body.GetRawText());
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    public async Task AnonymousDenialsAreNotWrittenToTheAuditTrail(int status)
    {
        var scopes = new CountingScopes();
        var (result, _) = await Run(new Exception("unused"), status, scopes);
        Assert.Equal(status, result);
        Assert.Equal(0, scopes.Calls);
    }
}
