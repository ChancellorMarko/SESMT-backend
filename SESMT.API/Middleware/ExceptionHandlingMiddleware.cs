using SESMT.API.Contracts.Errors;
using SESMT.Application.Exceptions;
using SESMT.Application.Modules.Appointments;
using SESMT.Application.Modules.Documents.DTOs;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Exceptions;
using Npgsql;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace SESMT.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var (status, code) = Classify(exception);
            // Client errors (validation, permission, conflict) are expected traffic: warning without the stack
            // trace. Only failures the client cannot cause or fix are logged as errors.
            if (status >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Erro inesperado em {Method} {Path} ({TraceId}).", context.Request.Method, context.Request.Path, context.TraceIdentifier);
            else
                _logger.LogWarning("Requisição recusada em {Method} {Path}: {Status} {Code} ({TraceId}).", context.Request.Method, context.Request.Path, status, code, context.TraceIdentifier);

            if (!context.Response.HasStarted)
            {
                await HandleExceptionAsync(context, exception);
            }
        }
        finally
        {
            // Anonymous traffic is not written to the audit trail: it would let anyone grow the table at will.
            // Failed logins of real accounts are audited by the login service.
            if (currentUser.UserId is not null && context.Response.StatusCode is StatusCodes.Status400BadRequest
                or StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound
                or StatusCodes.Status409Conflict
                or StatusCodes.Status422UnprocessableEntity)
            {
                await RecordDeniedAttemptAsync(context, currentUser);
            }
        }
    }

    // ─── Mapping exception → status + code ───────────────────────────────
    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code) = Classify(exception);

        var message = ClientMessage(exception, statusCode, code);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Code: code,
            Message: message,
            RequestId: context.TraceIdentifier,
            Issues: exception is WorkPermitNotReadyException pending ? pending.Issues : null);

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int StatusCode, string Code) Classify(Exception exception) => exception switch
    {
        DocumentOperationException operation
            => (operation.Status, CodeForStatus(operation.Status)),

        InvalidAppointmentInviteException
            => (StatusCodes.Status401Unauthorized, "INVALID_INVITE"),

        IdempotencyConflictException
            => (StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT"),

        NotFoundException
            => (StatusCodes.Status404NotFound, "NOT_FOUND"),

        WorkPermitForbiddenException
            => (StatusCodes.Status403Forbidden, "FORBIDDEN"),

        ResourceForbiddenException
            => (StatusCodes.Status403Forbidden, "FORBIDDEN"),

        WorkPermitNotReadyException
            => (StatusCodes.Status422UnprocessableEntity, "PENDING_REQUIREMENTS"),

        UnauthorizedAccessException
            => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),

        DomainException
            => (StatusCodes.Status409Conflict, "DOMAIN_VIOLATION"),

        DbUpdateConcurrencyException
            => (StatusCodes.Status409Conflict, "CONFLICT"),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
            => (StatusCodes.Status409Conflict, "DUPLICATE"),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }
            => (StatusCodes.Status409Conflict, "REFERENCE_VIOLATION"),

        DbUpdateException
            => (StatusCodes.Status409Conflict, "CONFLICT"),

        ArgumentException
            => (StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),

        _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")
    };

    /// <summary>Messages for the client. Persistence errors never echo provider text (table, constraint or SQL details).</summary>
    private static string ClientMessage(Exception exception, int statusCode, string code) => exception switch
    {
        _ when statusCode >= StatusCodes.Status500InternalServerError => "Ocorreu um erro inesperado.",
        DbUpdateConcurrencyException => "O registro foi alterado por outra pessoa. Atualize e tente novamente.",
        DbUpdateException when code == "DUPLICATE" => "Já existe um registro com esses dados.",
        DbUpdateException when code == "REFERENCE_VIOLATION" => "O registro faz referência a um item inexistente ou em uso.",
        DbUpdateException => "Não foi possível gravar a alteração. Atualize e tente novamente.",
        // ArgumentException appends " (Parameter 'name')", an internal detail.
        ArgumentException { ParamName: not null } argument => argument.Message.Replace($" (Parameter '{argument.ParamName}')", ""),
        _ => exception.Message,
    };

    // Códigos estáveis para os status usados por DocumentOperationException.
    private static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "VALIDATION_FAILED",
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status413PayloadTooLarge => "PAYLOAD_TOO_LARGE",
        StatusCodes.Status422UnprocessableEntity => "BUSINESS_RULE_VIOLATION",
        _ => "DOCUMENT_OPERATION_FAILED"
    };

    // ─── Auditing de tentativas negadas (4xx) — inalterado ──────────────
    private async Task RecordDeniedAttemptAsync(HttpContext context, ICurrentUserService currentUser)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SesmtDbContext>();

            var targetId = context.Request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => Guid.TryParse(segment, out var id) ? id : (Guid?)null)
                .FirstOrDefault(id => id.HasValue);

            var companyId = targetId.HasValue
                ? await ResolveCompanyIdAsync(db, targetId.Value)
                : null;

            var previousHash = await db.AuditLogs
                .OrderByDescending(log => log.Id)
                .Select(log => log.CurrentHash)
                .FirstOrDefaultAsync(CancellationToken.None);

            var log = AuditLog.ForCustom(
                action: "TENTATIVA_NEGADA",
                resourceType: "requisicao_http",
                resourceId: targetId,
                actorId: currentUser.UserId,
                actorEmail: currentUser.Email,
                actorName: currentUser.Name,
                previousData: null,
                newData: null,
                context: null,
                previousHash: previousHash,
                ipAddress: currentUser.IpAddress,
                userAgent: currentUser.UserAgent,
                companyId: companyId,
                result: $"HTTP_{context.Response.StatusCode}",
                traceId: context.TraceIdentifier);

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Não foi possível persistir o registro de tentativa negada.");
        }
    }

    private static async Task<Guid?> ResolveCompanyIdAsync(SesmtDbContext db, Guid targetId)
    {
        var companyId = await db.WorkPermits
            .Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId)
            .FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        companyId = await db.Documents
            .Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId)
            .FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        companyId = await db.Workers
            .Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId)
            .FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        return await db.Companies
            .Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(CancellationToken.None);
    }
}
