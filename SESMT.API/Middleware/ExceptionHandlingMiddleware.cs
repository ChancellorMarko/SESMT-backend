using SESMT.API.Contracts.Errors;
using SESMT.Application.Exceptions;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Exceptions;
using Npgsql;

namespace SESMT.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ocorreu uma excecao nao tratada durante o processamento da requisicao."
            );

            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleExceptionAsync(
    HttpContext context,
    Exception exception)
    {
        var statusCode = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            WorkPermitForbiddenException => StatusCodes.Status403Forbidden,
            WorkPermitNotReadyException => StatusCodes.Status422UnprocessableEntity,
            DomainException or DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } } => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Status: statusCode,
            Title: statusCode switch
            {
                StatusCodes.Status404NotFound => "Recurso nao encontrado",
                StatusCodes.Status403Forbidden => "Acesso negado",
                StatusCodes.Status400BadRequest => "Requisicao invalida",
                StatusCodes.Status409Conflict => "Conflito de estado",
                StatusCodes.Status422UnprocessableEntity => "Pendencias impedem a operacao",
                _ => "Erro interno"
            },
            Detail: exception is DbUpdateException
                ? "A operação conflita com uma regra de integridade dos dados."
                : statusCode is StatusCodes.Status400BadRequest
                or StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound
                or StatusCodes.Status409Conflict
                or StatusCodes.Status422UnprocessableEntity
                ? exception.Message
                : "Ocorreu um erro inesperado."
        );

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response)
        );
    }
}
