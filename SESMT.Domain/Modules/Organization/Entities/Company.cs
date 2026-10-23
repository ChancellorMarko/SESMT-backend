using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.ValueObjects;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Company : AggregateRootBase
{
    public CompanyType TipoEmpresa { get; private set; }
    public Cnpj Cnpj { get; private set; } = null!;
    public string CorporateName { get; private set; } = null!;
    public string FantasyName { get; private set; } = null!;
    public Email? Email { get; private set; }
    public string? Phone { get; private set; }
    public Guid? OrganizationId { get; private set; }

    protected Company() { }

    public Company(
        string corporateName,
        string fantasyName,
        string cnpj,
        string? email,
        string? phone,
        CompanyType tipoEmpresa,
        Guid? organizationId = null)
    {
        if (string.IsNullOrWhiteSpace(corporateName))
            throw new ArgumentException("Razão social é obrigatória.");
        if (string.IsNullOrWhiteSpace(fantasyName))
            throw new ArgumentException("Nome fantasia é obrigatório.");

        CorporateName = corporateName.Trim();
        FantasyName = fantasyName.Trim();
        Cnpj = new Cnpj(cnpj);   // valida e normaliza
        Email = string.IsNullOrWhiteSpace(email) ? null : new Email(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        TipoEmpresa = tipoEmpresa;
        OrganizationId = organizationId;
    }

    public void Update(
        string corporateName,
        string fantasyName,
        string cnpj,
        string? email,
        string? phone,
        CompanyType? tipoEmpresa = null)
    {
        if (string.IsNullOrWhiteSpace(corporateName))
            throw new ArgumentException("Razão social é obrigatória.");
        if (string.IsNullOrWhiteSpace(fantasyName))
            throw new ArgumentException("Nome fantasia é obrigatório.");

        CorporateName = corporateName.Trim();
        FantasyName = fantasyName.Trim();
        Cnpj = new Cnpj(cnpj);
        Email = string.IsNullOrWhiteSpace(email) ? null : new Email(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        if (tipoEmpresa.HasValue)
            TipoEmpresa = tipoEmpresa.Value;

        MarkAsUpdated();
    }

    public void Deactivate() => MarkAsDeleted();
}
