using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Company : AggregateRootBase
{
    public string Cnpj { get; private set; }
    public string CorporateName { get; private set; }
    public string FantasyName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    protected Company() { }

    public Company(string corporateName, string fantasyName, string cnpj, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(corporateName))
        {
            throw new ArgumentException("Razão social da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(fantasyName))
        {
            throw new ArgumentException("Nome fantasia da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            throw new ArgumentException("CNPJ da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Telefone da empresa é obrigatório.");
        }

        Cnpj = cnpj.Trim();
        CorporateName = corporateName.Trim();
        FantasyName = fantasyName.Trim();
        Email = email.ToLower();
        Phone = phone.Trim();
    }

    public void Update(string corporateName, string fantasyName, string cnpj, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(corporateName))
        {
            throw new ArgumentException("Razão social da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(fantasyName))
        {
            throw new ArgumentException("Nome fantasia da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            throw new ArgumentException("CNPJ da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Telefone da empresa é obrigatório.");
        }

        CorporateName = corporateName.Trim();
        FantasyName = fantasyName.Trim();
        Cnpj = cnpj.Trim();
        Email = email.ToLower();
        Phone = phone.Trim();
        MarkAsUpdated();
    }
}