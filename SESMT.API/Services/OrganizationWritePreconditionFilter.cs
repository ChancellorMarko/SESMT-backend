using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SESMT.API.Controllers;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

/// <summary>Legacy PATCH routes share the editor's lock and version, rather than bypassing them.</summary>
public sealed class OrganizationWritePreconditionFilter(SesmtDbContext db, OrganizationEditStore store) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resource = context.RouteData.Values["controller"]?.ToString() switch {
            "Companies" => "companies", "Locations" => "units", "Departments" => "departments", "Positions" => "positions", _ => throw new InvalidOperationException("Unknown organization route.") };
        var id = (Guid)context.ActionArguments["id"]!; var ct = context.HttpContext.RequestAborted;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var before = await store.StateWithCompany(resource, await store.Load(resource,id,true,ct),ct);
        var headers = context.HttpContext.Request.Headers.IfMatch;
        if (headers.Count == 0) { context.Result = Error(428,"PRECONDITION_REQUIRED","Consulte a versão em organization-management e envie If-Match antes de editar."); return; }
        var header = headers.Count == 1 ? headers[0] : null;
        if (header is not { Length:66 } || header[0] != '"' || header[^1] != '"' || !OrganizationEditingController.ValidVersion(header[1..^1])) {
            context.Result = Error(400,"INVALID_PRECONDITION","If-Match deve conter uma única versão entre aspas."); return;
        }
        if (header[1..^1] != before.Version) { context.Result = Error(409,"ORGANIZATION_CHANGED","O cadastro foi alterado. Atualize a consulta."); return; }
        var result = await next();
        if (result.Exception != null || result.Result is ObjectResult { StatusCode: >= 400 } || result.Result is StatusCodeResult { StatusCode: >= 400 }) return;
        var after = await store.StateWithCompany(resource,await store.Load(resource,id,false,ct),ct);
        if (before.CompanyId != after.CompanyId || before.UnitId != after.UnitId || resource == "companies"
            && (before.Values.GetProperty("cnpj").GetString() != after.Values.GetProperty("cnpj").GetString()
                || before.Values.GetProperty("tipoEmpresa").GetString() != after.Values.GetProperty("tipoEmpresa").GetString())) {
            context.Result = Error(400,"IMMUTABLE_IDENTITY","Preserve a identidade e o vínculo do cadastro.");
            result.Result = context.Result; return;
        }
        await store.Audit(before,after,null,ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        context.HttpContext.Response.Headers.ETag = $"\"{after.Version}\"";
        ObjectResult Error(int status,string code,string message) => new(new { code,message,requestId = context.HttpContext.TraceIdentifier }) { StatusCode=status };
    }
}
