using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Documents.DTOs;

namespace SESMT.API.Controllers;

/// <summary>
/// Converte falhas documentais esperadas em respostas no contrato padrão da API
/// (<see cref="ErrorResponse"/>). Demais exceções seguem para o middleware global.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DocumentExceptionsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            DocumentOperationException failure => failure.Status,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _ => 0,
        };
        if (status == 0) return;

        var code = status switch
        {
            StatusCodes.Status400BadRequest => "VALIDATION_FAILED",
            StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            StatusCodes.Status404NotFound => "NOT_FOUND",
            StatusCodes.Status409Conflict => "CONFLICT",
            StatusCodes.Status413PayloadTooLarge => "PAYLOAD_TOO_LARGE",
            StatusCodes.Status422UnprocessableEntity => "BUSINESS_RULE_VIOLATION",
            _ => "DOCUMENT_OPERATION_FAILED",
        };

        context.Result = new ObjectResult(new ErrorResponse(
            Code: code,
            Message: context.Exception.Message,
            RequestId: context.HttpContext.TraceIdentifier))
        { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
