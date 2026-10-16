using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Domain.Modules.Organization.Repositories;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByCnpjAsync(string cnpj, CancellationToken ct = default);

    Task<bool> ExistsByCnpjAsync(string cnpj, Guid ignoreId, CancellationToken ct = default);

    Task<IReadOnlyList<Company>> ListAsync(
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Company company, CancellationToken ct = default);

    void Update(Company company);
}