using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Application.Modules.Organization.Mappers;

public static class CompanyMapper
{
    public static Company ToEntity(CompanyCreateRequest request) =>
        new(
            corporateName: request.CorporateName,
            fantasyName: request.FantasyName,
            cnpj: request.Cnpj,
            email: request.Email,
            phone: request.Phone,
            tipoEmpresa: request.TipoEmpresa,
            organizationId: request.OrganizationId);

    public static CompanyResponse ToResponse(Company company) =>
        new()
        {
            Id = company.Id,
            CorporateName = company.CorporateName,
            FantasyName = company.FantasyName,
            Cnpj = company.Cnpj,
            Email = company.Email,
            Phone = company.Phone,
            OrganizationId = company.OrganizationId,
            TipoEmpresa = company.TipoEmpresa.ToString(),
            Active = company.Active && company.DeletedAt is null
        };

    public static CompanySummaryResponse ToSummary(Company company) =>
        new()
        {
            Id = company.Id,
            CorporateName = company.CorporateName,
            FantasyName = company.FantasyName,
            TipoEmpresa = company.TipoEmpresa.ToString(),
            Active = company.Active && company.DeletedAt is null
        };
}