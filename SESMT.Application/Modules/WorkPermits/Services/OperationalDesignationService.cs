using SESMT.Application.Exceptions;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class OperationalDesignationService(IOperationalDesignationRepository repository)
{
    public async Task<OperationalDesignationResponse> CreateAsync(CreateOperationalDesignationRequest request, Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var services = WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds);
        _ = services;
        var companies = await repository.GetScopedCompanyIdsAsync(actorId, ct);
        var companyId = await repository.GetActiveWorkerCompanyIdAsync(request.PersonId, companies, ct)
            ?? throw new NotFoundException("Pessoa não encontrada no escopo da sessão.");
        if (!await repository.HasRoleInCompanyAsync(actorId, companyId, WorkPermitRoleCodes.Configurator, ct))
            throw new WorkPermitForbiddenException();
        if (request.LocationIds.Count == 0 || request.LocationIds.Distinct().Count() != request.LocationIds.Count
            || !await repository.AreActiveLocationsInCompanyAsync(request.LocationIds, companyId, ct))
            throw new NotFoundException("Local não encontrado no escopo da sessão.");

        var item = new OperationalDesignation(companyId, request.PersonId, request.ServiceIds, request.LocationIds,
            request.Type, request.StartsAt, request.EndsAt, request.Origin, actorId);
        var duplicate = await repository.FindDuplicateAsync(item, ct);
        if (duplicate is not null) return OperationalDesignationResponse.From(duplicate);
        await repository.AddAsync(item, ct);
        await repository.SaveChangesAsync(ct);
        return OperationalDesignationResponse.From(item);
    }

    public async Task<IReadOnlyList<OperationalDesignationResponse>> ListAsync(Guid actorId, CancellationToken ct)
    {
        var companies = await repository.GetScopedCompanyIdsAsync(actorId, ct);
        var authorized = new List<Guid>();
        foreach (var companyId in companies)
            if (await repository.HasRoleInCompanyAsync(actorId, companyId, WorkPermitRoleCodes.Configurator, ct)) authorized.Add(companyId);
        if (authorized.Count == 0) throw new WorkPermitForbiddenException();
        var rows = await repository.ListAsync(authorized, ct);
        var valid = new List<OperationalDesignationResponse>();
        foreach (var row in rows)
        {
            if (await repository.GetActiveWorkerCompanyIdAsync(row.PersonId, [row.CompanyId], ct) != row.CompanyId
                || !await repository.AreActiveLocationsInCompanyAsync(row.LocationIds, row.CompanyId, ct)) continue;
            try { _ = WorkPermitServiceCatalogEntry.ResolveServiceCodes(row.ServiceIds); }
            catch (ArgumentException) { continue; }
            valid.Add(OperationalDesignationResponse.From(row));
        }
        return valid;
    }

    public async Task<OperationalDesignationResponse> RevokeAsync(Guid id, RevokeOperationalDesignationRequest request, Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 1) throw new ArgumentException("expectedVersion é obrigatório.");
        var companies = await repository.GetScopedCompanyIdsAsync(actorId, ct);
        var designation = await repository.GetAsync(id, companies, ct) ?? throw new NotFoundException("Designação não encontrada.");
        if (!await repository.HasRoleInCompanyAsync(actorId, designation.CompanyId, WorkPermitRoleCodes.Configurator, ct))
            throw new WorkPermitForbiddenException();
        designation.Revoke(actorId, request.ExpectedVersion);
        await repository.SaveChangesAsync(ct);
        return OperationalDesignationResponse.From(designation);
    }
}
