using SESMT.Application.Common;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.Organization.Repositories;
using SESMT.Domain.ValueObjects;

namespace SESMT.Application.Modules.Organization.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CompanyService(ICompanyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyResponse>> CreateAsync(
    CompanyCreateRequest request,
    CancellationToken cancellationToken = default)
    {
        if (!Cnpj.IsValid(request.Cnpj))
            return Result<CompanyResponse>.Failure("CNPJ inválido.");

        var cnpj = Cnpj.Normalize(request.Cnpj);

        if (await _repository.ExistsByCnpjAsync(cnpj, cancellationToken))
            return Result<CompanyResponse>.Failure("Já existe uma empresa com esse CNPJ.");

        try
        {
            var company = CompanyMapper.ToEntity(request);
            await _repository.AddAsync(company, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<CompanyResponse>.Success(CompanyMapper.ToResponse(company));
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<CompanyResponse>> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(companyId, cancellationToken);

        return company is null
            ? Result<CompanyResponse>.Failure("Empresa não encontrada.")
            : Result<CompanyResponse>.Success(CompanyMapper.ToResponse(company));
    }

    public async Task<Result<IEnumerable<CompanySummaryResponse>>> ListAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        var companies = await _repository.ListAsync(skip, pageSize, cancellationToken);

        var dtos = companies.Select(CompanyMapper.ToSummary).ToList();
        return Result<IEnumerable<CompanySummaryResponse>>.Success(dtos);
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(
        Guid companyId,
        CompanyCreateRequest updateData,
        CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return Result<CompanyResponse>.Failure("Empresa não encontrada.");

        // Regra: CNPJ único (ignorando a própria empresa)
        if (await _repository.ExistsByCnpjAsync(updateData.Cnpj, companyId, cancellationToken))
            return Result<CompanyResponse>.Failure(
                "Já existe outra empresa com esse CNPJ.");

        company.Update(
            corporateName: updateData.CorporateName,
            fantasyName: updateData.FantasyName,
            cnpj: updateData.Cnpj,
            email: updateData.Email,
            phone: updateData.Phone,
            tipoEmpresa: updateData.TipoEmpresa);

        _repository.Update(company);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompanyResponse>.Success(CompanyMapper.ToResponse(company));
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return Result<bool>.Failure("Empresa não encontrada.");

        company.Deactivate();

        _repository.Update(company);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
