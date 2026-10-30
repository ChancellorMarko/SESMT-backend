using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Browser;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("browser/workers")]
public sealed class BrowserWorkersController(IBrowserWorkspaceAccessReader accessReader, IBrowserWorkersReader workers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var (access, failure) = await Access(cancellationToken);
        if (failure is not null) return failure;
        if (!ValidKeys("unitId", "companyId", "q", "status", "page", "pageSize")
            || !TryGuid("unitId", true, out var unitId) || !TryGuid("companyId", false, out var companyId)
            || !TryPage(10, out var page, out var pageSize))
            return Invalid("Informe filtros válidos para a consulta de trabalhadores.");
        if (!access!.HasUnit(unitId!.Value) || companyId.HasValue && !access.Can("workers.read", companyId.Value))
            return Failure(403, "FORBIDDEN", "Unidade ou empresa fora do seu acesso.");
        if (access.UnitCompanies is not null && access.UnitCompanies.TryGetValue(unitId.Value, out var unitCompany)
            && (!access.Can("workers.read", unitCompany) || companyId.HasValue && unitCompany != companyId.Value))
            return Failure(403, "FORBIDDEN", "Unidade ou empresa fora do seu acesso.");
        var rawSearch = Request.Query["q"].ToString();
        var search = rawSearch.Trim();
        var status = Request.Query["status"].ToString();
        if (rawSearch.Length > 200 || rawSearch.Any(char.IsControl) || status is not ("" or "ACTIVE" or "TERMINATED"))
            return Invalid("Informe uma busca de até 200 caracteres e um estado válido.");
        return Ok(await workers.ListAsync(access, new(unitId.Value, companyId, search, status, page, pageSize), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var (access, failure) = await Access(cancellationToken);
        if (failure is not null) return failure;
        if (!ValidKeys("unitId") || !TryGuid("unitId", false, out var unitId)) return Invalid("Informe uma unidade válida.");
        if (unitId.HasValue && !access!.HasUnit(unitId.Value)) return Failure(403, "FORBIDDEN", "Unidade fora do seu acesso.");
        var worker = await workers.FindAsync(access!, id, cancellationToken);
        return Allowed(access!, worker) && (!unitId.HasValue || worker!.UnitId == unitId) ? Ok(worker) : Missing();
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id, CancellationToken cancellationToken)
    {
        var (access, failure) = await Access(cancellationToken);
        if (failure is not null) return failure;
        if (!ValidKeys("unitId", "page", "pageSize", "sort") || !TryGuid("unitId", false, out var unitId)
            || !TryPage(5, out var page, out var pageSize) || Request.Query["sort"].ToString() is not ("" or "effectiveDate:desc"))
            return Invalid("Informe filtros e ordenação válidos para o histórico.");
        if (unitId.HasValue && !access!.HasUnit(unitId.Value)) return Failure(403, "FORBIDDEN", "Unidade fora do seu acesso.");
        var worker = await workers.FindAsync(access!, id, cancellationToken);
        if (!Allowed(access!, worker) || unitId.HasValue && worker!.UnitId != unitId) return Missing();
        return Ok(await workers.MovementsAsync(access!, worker!, page, pageSize, cancellationToken));
    }

    private async Task<(BrowserWorkspaceAccess? Access, IActionResult? Failure)> Access(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return (null, Failure(401, "UNAUTHENTICATED", "Entre novamente para continuar."));
        var access = await accessReader.ReadAsync(userId, cancellationToken);
        return access is null || !access.CompanyIds.Any(company => access.Can("workers.read", company))
            ? (null, Failure(403, "FORBIDDEN", "Seu acesso não permite consultar trabalhadores.")) : (access, null);
    }

    private static bool Allowed(BrowserWorkspaceAccess access, BrowserWorkerResponse? worker) => worker is not null
        && access.Can("workers.read", worker.CompanyId) && access.AllowsLocation(worker.CompanyId, worker.UnitId, worker.SectorId)
        && (!access.OwnOnly || worker.Id == access.WorkerId);

    // Reject unsupported or repeated filters instead of silently changing the requested scope.
    private bool ValidKeys(params string[] keys) => Request.Query.All(pair => keys.Contains(pair.Key, StringComparer.Ordinal) && pair.Value.Count == 1);
    private bool TryGuid(string key, bool required, out Guid? value)
    {
        value = null;
        if (!Request.Query.TryGetValue(key, out var text)) return !required;
        if (!Guid.TryParseExact(text.ToString(), "D", out var parsed) || parsed == Guid.Empty) return false;
        value = parsed;
        return true;
    }
    private bool TryPage(int defaultSize, out int page, out int pageSize)
    {
        return TryPositive("page", 1, out page) & TryPositive("pageSize", defaultSize, out pageSize)
            && pageSize <= 100 && (long)(page - 1) * pageSize <= int.MaxValue;
    }
    private bool TryPositive(string key, int defaultValue, out int number)
    {
        number = defaultValue;
        return !Request.Query.TryGetValue(key, out var text)
            || int.TryParse(text.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    }
    private ObjectResult Invalid(string message) => Failure(422, "VALIDATION_FAILED", message);
    private ObjectResult Missing() => Failure(404, "NOT_FOUND", "Trabalhador não encontrado.");
    private ObjectResult Failure(int status, string code, string message) => StatusCode(status, new BrowserErrorResponse(code, message, HttpContext.TraceIdentifier));
}
