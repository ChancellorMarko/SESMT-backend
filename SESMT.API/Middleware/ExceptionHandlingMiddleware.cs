using SESMT.API.Contracts.Errors;
using SESMT.Application.Exceptions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Exceptions;
using Npgsql;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
<<<<<<< HEAD
using SESMT.Domain.Modules.Audit.Constants;
=======
>>>>>>> main
using SESMT.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace SESMT.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
<<<<<<< HEAD
=======

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
>>>>>>> main

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
            _logger.LogError(
                exception,
                "Ocorreu uma excecao nao tratada durante o processamento da requisicao.");

            if (!context.Response.HasStarted)
            {
                await HandleExceptionAsync(context, exception);
            }
        }
        finally
        {
            if (context.Response.StatusCode is StatusCodes.Status400BadRequest
                or StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound
                or StatusCodes.Status409Conflict
                or StatusCodes.Status422UnprocessableEntity)
            {
                await RecordDeniedAttemptAsync(context, currentUser);
            }
        }
        finally
        {
            if (context.Response.StatusCode is StatusCodes.Status400BadRequest
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
            var previousHash = await db.AuditLogs.OrderByDescending(log => log.Id)
                .Select(log => log.CurrentHash).FirstOrDefaultAsync(CancellationToken.None);
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
        var companyId = await db.WorkPermits.Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId).FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        companyId = await db.Documents.Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId).FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        companyId = await db.Workers.Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.CompanyId).FirstOrDefaultAsync(CancellationToken.None);
        if (companyId.HasValue) return companyId;

        return await db.Companies.Where(item => item.Id == targetId)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(CancellationToken.None);
    }

    // ─── Mapping exception → status + code ───────────────────────────────
    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
<<<<<<< HEAD
        var statusCode = exception switch
        {
            SESMT.Application.Modules.Documents.DTOs.DocumentOperationException operation => operation.Status,
            SESMT.Application.Modules.Appointments.InvalidAppointmentInviteException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            WorkPermitForbiddenException => StatusCodes.Status403Forbidden,
            WorkPermitNotReadyException => StatusCodes.Status422UnprocessableEntity,
            DomainException or DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } } => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
=======
        var (statusCode, code) = Classify(exception);

        var message = statusCode == StatusCodes.Status500InternalServerError
            ? "Ocorreu um erro inesperado."
            : exception.Message;
>>>>>>> main

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
<<<<<<< HEAD
            Status: statusCode,
            Title: statusCode switch
            {
                StatusCodes.Status404NotFound => "Recurso nao encontrado",
                StatusCodes.Status401Unauthorized => "Convite invalido",
                StatusCodes.Status413PayloadTooLarge => "Arquivo excede o limite",
                StatusCodes.Status403Forbidden => "Acesso negado",
                StatusCodes.Status400BadRequest => "Requisicao invalida",
                StatusCodes.Status409Conflict => "Conflito de estado",
                StatusCodes.Status422UnprocessableEntity => "Pendencias impedem a operacao",
                _ => "Erro interno"
            },
            Detail: exception is DbUpdateException
                ? "A operação conflita com uma regra de integridade dos dados."
                : statusCode is StatusCodes.Status400BadRequest
                or StatusCodes.Status401Unauthorized
                or StatusCodes.Status413PayloadTooLarge
                or StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound
                or StatusCodes.Status409Conflict
                or StatusCodes.Status422UnprocessableEntity
                ? exception.Message
                : "Ocorreu um erro inesperado."
        );
=======
            Code: code,
            Message: message,
            RequestId: context.TraceIdentifier);
>>>>>>> main

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int StatusCode, string Code) Classify(Exception exception) => exception switch
    {
        NotFoundException
            => (StatusCodes.Status404NotFound, "NOT_FOUND"),

        WorkPermitForbiddenException
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
