using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Domain.Common.Result;

namespace SESMT.API.Controllers;

/// <summary>
/// Base para todos os controllers. Converte Result<T>
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(Result<T> result) where T : notnull
        => result.IsSuccess ? Ok(result.Value) : Failure(result.Error!);

    protected IActionResult CreatedFromResult<T>(Result<T> result, string actionName, object routeValues) where T : notnull
        => result.IsSuccess
            ? CreatedAtAction(actionName, routeValues, result.Value)
            : Failure(result.Error!);

    protected IActionResult NoContentFromResult<T>(Result<T> result) where T : notnull
        => result.IsSuccess ? NoContent() : Failure(result.Error!);

    protected IActionResult Failure(string error)
    {
        var (status, code) = Classify(error);

        return StatusCode(status, new ErrorResponse(
            Code: code,
            Message: error,
            RequestId: HttpContext.TraceIdentifier));
    }

    protected IActionResult Failure(int status, string code, string message)
        => StatusCode(status, new ErrorResponse(code, message, HttpContext.TraceIdentifier));

    private static (int Status, string Code) Classify(string error)
    {
        var e = error.ToLowerInvariant();

        if (e.Contains("não encontrad") || e.Contains("not found"))
            return (404, "NOT_FOUND");

        if (e.Contains("já existe") || e.Contains("duplicad"))
            return (409, "DUPLICATE");

        if (e.Contains("não é permitido") || e.Contains("não pode")
            || e.Contains("só é possível") || e.Contains("vínculo")
            || e.Contains("prestadora") || e.Contains("regra"))
            return (422, "BUSINESS_RULE_VIOLATION");

        return (400, "VALIDATION_FAILED");
    }
}
