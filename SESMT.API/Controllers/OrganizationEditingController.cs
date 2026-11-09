using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizationEditRequest(string ExpectedVersion, string Reason, bool Confirmation, JsonElement Values);
public sealed record CompanyEditValues(string CorporateName, string FantasyName, string Cnpj, string TipoEmpresa, string? Email, string? Phone);
public sealed record UnitEditValues(Guid CompanyId, string Code, string Name, string Type, string? Cep, string? Address, string? Number, string? Complement, string? Neighborhood, string? City, string? State);
public sealed record DepartmentEditValues(Guid UnitId, string Name, string? Description, Guid? ResponsibleId);
public sealed record PositionEditValues(Guid CompanyId, string Name, string? Description, string? Cbo);

[ApiController, Authorize, Route("organization-management/{resource}/{id:guid}")]
public sealed class OrganizationEditingController(SesmtDbContext db, OrganizationEditStore store) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(string resource, Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var state = await store.StateWithCompany(resource, await store.Load(resource, id, false, ct), ct);
        Response.Headers.ETag = $"\"{state.Version}\"";
        return Ok(state);
    }
    [HttpGet("history")]
    public async Task<IActionResult> History(string resource, Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var current = await store.StateWithCompany(resource, await store.Load(resource, id, false, ct), ct);
        var rows = await db.AuditLogs.AsNoTracking().Where(a => a.ResourceType == "organizacao" && a.ResourceId == id && a.CompanyId == current.CompanyId)
            .OrderByDescending(a => a.Id).Take(100).Select(a => new { a.EventId, a.ActorName, a.OccurredAt, a.NewData }).ToArrayAsync(ct);
        return Ok(rows.Select(a => { using var json = JsonDocument.Parse(a.NewData!); return new { id = a.EventId, a.ActorName, a.OccurredAt,
            version = json.RootElement.GetProperty("version").GetString(), reason = json.RootElement.GetProperty("reason").GetString() }; }));
    }
    [HttpPatch]
    public async Task<IActionResult> Edit(string resource, Guid id, OrganizationEditRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!request.Confirmation || !ValidVersion(request.ExpectedVersion) || request.Reason?.Trim().Length is not (>= 3 and <= 200) || request.Values.ValueKind != JsonValueKind.Object)
            return Failure(400, "VALIDATION_FAILED", "Confira os campos, motivo, confirmação e versão do cadastro.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = await store.Load(resource, id, true, ct);
        var before = await store.StateWithCompany(resource, item, ct);
        if (!item.Active || item.DeletedAt.HasValue || before.Version != request.ExpectedVersion) return Changed();
        try {
            switch (item) {
                case Company c:
                    var company = request.Values.Deserialize<CompanyEditValues>(OrganizationEditStore.Json)!;
                    if (company == null || company.Cnpj != c.Cnpj.Value || company.TipoEmpresa != c.TipoEmpresa.ToString()
                        || !Text(company.CorporateName,255) || !Text(company.FantasyName,255) || !Optional(company.Email,200) || !Optional(company.Phone,20)) return Invalid();
                    c.Update(company.CorporateName, company.FantasyName, c.Cnpj.Value, OptionalText(company.Email), OptionalText(company.Phone)); break;
                case Unit u:
                    var unit = request.Values.Deserialize<UnitEditValues>(OrganizationEditStore.Json)!;
                    if (unit == null || unit.CompanyId != u.CompanyId || !Text(unit.Name,150) || !Text(unit.Code,50) || !Text(unit.Type,30)
                        || unit.Cep is not { Length:8 } || !unit.Cep.All(char.IsAsciiDigit) || !Text(unit.Address,200) || !Text(unit.Number,20) || !Optional(unit.Complement,100)
                        || !Text(unit.Neighborhood,100) || !Text(unit.City,120) || !"AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO".Split(' ').Contains(unit.State)) return Invalid();
                    u.Update(unit.Code,unit.Name,unit.Type,unit.Cep,unit.Address,unit.Number,OptionalText(unit.Complement),unit.Neighborhood,unit.City,unit.State); break;
                case Department d:
                    var department = request.Values.Deserialize<DepartmentEditValues>(OrganizationEditStore.Json)!;
                    if (department == null || department.UnitId != d.UnitId || department.ResponsibleId != d.ResponsibleId || !Text(department.Name,150) || !Optional(department.Description,500)) return Invalid();
                    d.Update(department.Name,OptionalText(department.Description),d.ResponsibleId); break;
                case Position p:
                    var position = request.Values.Deserialize<PositionEditValues>(OrganizationEditStore.Json)!;
                    if (position == null || position.CompanyId != p.CompanyId || !Text(position.Name,150) || !Optional(position.Description,500) || !Optional(position.Cbo,10)) return Invalid();
                    p.Update(position.Name,OptionalText(position.Cbo),OptionalText(position.Description)); break;
            }
        } catch (JsonException) { return Invalid(); }
        try {
            await db.SaveChangesAsync(ct);
            var after = await store.StateWithCompany(resource, await store.Load(resource,id,false,ct), ct);
            await store.Audit(before,after,request.Reason.Trim(),ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            Response.Headers.ETag = $"\"{after.Version}\""; return Ok(after);
        } catch (DbUpdateConcurrencyException) { return Changed(); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState:"23505" }) { return Failure(409,"DUPLICATE_REGISTRATION","Já existe um cadastro com este nome ou código no mesmo vínculo."); }
    }
    public static bool ValidVersion(string? value) => value is { Length:64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool Optional(string? value,int max) => value == null || value.Trim().Length <= max;
    private static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private IActionResult Invalid() => Failure(400,"VALIDATION_FAILED","Confira os campos editáveis. A identidade e o vínculo do cadastro devem ser preservados.");
    private IActionResult Changed() => Failure(409,"ORGANIZATION_CHANGED","O cadastro foi alterado. Atualize a consulta antes de salvar novamente.");
}
