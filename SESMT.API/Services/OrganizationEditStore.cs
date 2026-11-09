using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record OrganizationEditState(string Resource, Guid Id, Guid CompanyId, Guid? UnitId, string Version, JsonElement Values);

/// <summary>Content version plus a row lock: all fields and the persisted update instant participate.</summary>
public sealed class OrganizationEditStore(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static string Permission(string resource) => resource switch { "companies" => "empresa", "units" => "unidade", "departments" => "setor", "positions" => "cargo", _ => throw new NotFoundException("Cadastro não encontrado.") };

    public async Task<AggregateRootBase> Load(string resource, Guid id, bool locked, CancellationToken ct)
    {
        _ = Permission(resource);
        var s = await access.ReadAsync(ct);
        if (s.OwnOnly) throw new ResourceForbiddenException();
        AggregateRootBase? entity = resource switch {
            "companies" => await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Active && x.DeletedAt == null, ct),
            "units" => await db.Units.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Active && x.DeletedAt == null, ct),
            "departments" => await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Active && x.DeletedAt == null, ct),
            _ => await db.Positions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Active && x.DeletedAt == null, ct)
        };
        if (entity is null) throw new NotFoundException("Cadastro não encontrado.");
        var company = entity switch { Company c => c.Id, Unit u => u.CompanyId, Position p => p.CompanyId,
            Department d => await db.Units.Where(u => u.Id == d.UnitId && u.Active && u.DeletedAt == null).Select(u => u.CompanyId).SingleOrDefaultAsync(ct), _ => Guid.Empty };
        await access.RequireAsync(Permission(resource) + ":ler", company, false, ct);
        await access.RequireAsync(Permission(resource) + ":editar", company, entity is Company or Position, ct);
        if (!await db.Companies.AnyAsync(c => c.Id == company && c.Active && c.DeletedAt == null, ct)) throw new ResourceForbiddenException();
        if (entity is Unit unit && !s.FullUnitIds.Contains(unit.Id) || entity is Department department && !s.FullUnitIds.Contains(department.UnitId)) throw new ResourceForbiddenException();
        if (!locked) return entity;
        // Identifiers below are fixed in code; only the ID is supplied by the caller.
        return resource switch {
            "companies" => await db.Companies.FromSqlInterpolated($"SELECT * FROM core.empresa WHERE id = {id} FOR UPDATE").SingleAsync(ct),
            "units" => await db.Units.FromSqlInterpolated($"SELECT * FROM core.unidade WHERE id = {id} FOR UPDATE").SingleAsync(ct),
            "departments" => await db.Departments.FromSqlInterpolated($"SELECT * FROM core.setor WHERE id = {id} FOR UPDATE").SingleAsync(ct),
            _ => await db.Positions.FromSqlInterpolated($"SELECT * FROM core.cargo WHERE id = {id} FOR UPDATE").SingleAsync(ct)
        };
    }

    public static OrganizationEditState State(string resource, AggregateRootBase item)
    {
        var (company, unit, values) = item switch {
            Company c => (c.Id, (Guid?)null, (object)new { c.CorporateName, c.FantasyName, cnpj = c.Cnpj.Value, tipoEmpresa = c.TipoEmpresa.ToString(), email = c.Email?.Value, c.Phone }),
            Unit u => (u.CompanyId, (Guid?)u.Id, (object)new { u.CompanyId, u.Code, u.Name, u.Type, cep = u.CEP, u.Address, u.Number, u.Complement, u.Neighborhood, u.City, u.State }),
            Department d => (Guid.Empty, (Guid?)d.UnitId, (object)new { d.UnitId, d.Name, d.Description, d.ResponsibleId }),
            Position p => (p.CompanyId, (Guid?)null, (object)new { p.CompanyId, p.Name, p.Description, cbo = p.CBO }),
            _ => throw new ArgumentException("Cadastro inválido.")
        };
        var json = JsonSerializer.SerializeToElement(values, Json);
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { resource, item.Id, item.UpdatedAt, item.Active, item.DeletedAt, values = json }, Json))));
        return new(resource, item.Id, company, unit, version, json);
    }
    public async Task<OrganizationEditState> StateWithCompany(string resource, AggregateRootBase item, CancellationToken ct)
    {
        var state = State(resource, item);
        return item is Department d ? state with { CompanyId = await db.Units.Where(u => u.Id == d.UnitId).Select(u => u.CompanyId).SingleAsync(ct) } : state;
    }
    public async Task Audit(OrganizationEditState before, OrganizationEditState after, string? reason, CancellationToken ct)
    {
        async Task<string> Canonical(object value) => await db.Database.SqlQuery<string>($"SELECT CAST(CAST({JsonSerializer.Serialize(value, Json)} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        db.Add(new AuditLog("ORGANIZATION_UPDATED", "organizacao", after.Id, actor.UserId, actor.Email, actor.Name,
            await Canonical(new { before.Resource, before.Version, before.Values }), await Canonical(new { after.Resource, after.Version, after.Values, reason }),
            null, actor.IpAddress, actor.UserAgent, await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct),
            companyId: after.CompanyId, result: "SUCCESS", traceId: actor.TraceId));
    }
}
