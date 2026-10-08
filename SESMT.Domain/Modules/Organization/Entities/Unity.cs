using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Unit : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public string? CEP { get; private set; }
    public string? Address { get; private set; }
    public string? Number { get; private set; }
    public string? Complement { get; private set; }
    public string? Neighborhood { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }

    protected Unit() { }

    public Unit(Guid companyId, string code, string name, string type, string? cep, string? address, string? number, string? complement, string? neighborhood, string? city, string? state)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("ID da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Código da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Tipo da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(cep))
        {
            throw new ArgumentException("CEP da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Endereço da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("Número do endereço da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(neighborhood))
        {
            throw new ArgumentException("Bairro da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("Cidade da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("Estado da unidade é obrigatório.");
        }

        CompanyId = companyId;
        Code = code.Trim();
        Name = name.Trim();
        Type = type.Trim();
        CEP = cep.Trim();
        Address = address.Trim();
        Number = number.Trim();
        Complement = complement?.Trim();
        Neighborhood = neighborhood.Trim();
        City = city.Trim();
        State = state.Trim();
    }

    public void Update(string code, string name, string type, string? cep, string? address, string? number, string? complement, string? neighborhood, string? city, string? state)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Código da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Tipo da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(cep))
        {
            throw new ArgumentException("CEP da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Endereço da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("Número do endereço da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(neighborhood))
        {
            throw new ArgumentException("Bairro da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("Cidade da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("Estado da unidade é obrigatório.");
        }

        Code = code.Trim();
        Name = name.Trim();
        Type = type.Trim();
        CEP = cep?.Trim();
        Address = address?.Trim();
        Number = number?.Trim();
        Complement = complement?.Trim();
        Neighborhood = neighborhood?.Trim();
        City = city?.Trim();
        State = state?.Trim();
        MarkAsUpdated();
    }
}