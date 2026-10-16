using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Organization;

public class CompanyRepository : ICompanyRepository
{
    private readonly SesmtDbContext _db;

    public CompanyRepository(SesmtDbContext db) => _db = db;

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsByCnpjAsync(string cnpj, CancellationToken ct = default)
        => _db.Companies.AnyAsync(c => c.Cnpj == cnpj && c.DeletedAt == null, ct);

    public Task<bool> ExistsByCnpjAsync(string cnpj, Guid ignoreId, CancellationToken ct = default)
        => _db.Companies.AnyAsync(c =>
            c.Cnpj == cnpj &&
            c.Id != ignoreId &&
            c.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Company>> ListAsync(
        int skip,
        int take,
        CancellationToken ct = default)
        => await _db.Companies
            .Where(c => c.Active && c.DeletedAt == null)
            .OrderBy(c => c.CorporateName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await _db.Companies.AddAsync(company, ct);

    public void Update(Company company) => _db.Companies.Update(company);
}