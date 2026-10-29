using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Documents.DTOs;

namespace SESMT.API.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DocumentExceptionsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            DocumentOperationException failure => failure.Status,
            ArgumentException => 400,
            InvalidOperationException => 422,
            _ => 0,
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ErrorResponse(status, "Solicitação documental recusada", context.Exception.Message)) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
