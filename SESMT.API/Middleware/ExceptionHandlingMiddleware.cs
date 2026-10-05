using SESMT.API.Contracts.Errors;
using SESMT.Application.Exceptions;
using System.Net;
using System.Text.Json;

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
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Status: statusCode,
            Title: statusCode == StatusCodes.Status404NotFound
                ? "Recurso nao encontrado"
                : "Erro interno",
            Detail: statusCode == StatusCodes.Status404NotFound
                ? exception.Message
                : "Ocorreu um erro inesperado."
        );

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response)
        );
    }
}