using SESMT.Application.Common;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Application.Modules.Organization.Services;

public sealed class UnitService : IUnitService
{
    private readonly IUnitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UnitService(IUnitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UnitResponse>> CreateAsync(
        UnitCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Regra: código único por empresa
        var exists = await _repository.ExistsByCodeAsync(
            request.CompanyId, request.Code, cancellationToken);

        if (exists)
            return Result<UnitResponse>.Failure(
                "Já existe uma unidade com esse código nesta empresa.");

        try
        {
            var unit = UnitMapper.ToEntity(request);

            await _repository.AddAsync(unit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<UnitResponse>.Success(UnitMapper.ToResponse(unit));
        }
        catch (ArgumentException ex)
        {
            return Result<UnitResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<UnitResponse>> GetByIdAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetByIdAsync(unitId, cancellationToken);

        return unit is null
            ? Result<UnitResponse>.Failure("Unidade não encontrada.")
            : Result<UnitResponse>.Success(UnitMapper.ToResponse(unit));
    }

    public async Task<Result<IEnumerable<UnitSummaryResponse>>> ListByCompanyAsync(
        Guid companyId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var units = await _repository.ListByCompanyAsync(
            companyId, skip, pageSize, cancellationToken);

        var dtos = units.Select(UnitMapper.ToSummary).ToList();

        return Result<IEnumerable<UnitSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<IEnumerable<UnitSummaryResponse>>> ListAsync(
        Guid? companyId = null,
        string? code = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var units = await _repository.ListAsync(
            companyId, code, skip, pageSize, cancellationToken);

        var dtos = units.Select(UnitMapper.ToSummary).ToList();

        return Result<IEnumerable<UnitSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<UnitResponse>> UpdateAsync(
        Guid unitId,
        UnitUpdateRequest updateData,
        CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetByIdAsync(unitId, cancellationToken);
        if (unit is null)
            return Result<UnitResponse>.Failure("Unidade não encontrada.");

        // Se está mudando o código, verifica duplicidade
        if (!string.Equals(unit.Code, updateData.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicado = await _repository.ExistsByCodeAsync(
                unit.CompanyId, updateData.Code, cancellationToken);

            if (duplicado)
                return Result<UnitResponse>.Failure(
                    "Já existe uma unidade com esse código nesta empresa.");
        }

        try
        {
            unit.ChangeDetails(
                code: updateData.Code,
                name: updateData.Name,
                type: updateData.Type,
                cep: updateData.Cep,
                address: updateData.Address,
                number: updateData.Number,
                complement: updateData.Complement,
                neighborhood: updateData.Neighborhood,
                city: updateData.City,
                state: updateData.State);

            _repository.Update(unit);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<UnitResponse>.Success(UnitMapper.ToResponse(unit));
        }
        catch (ArgumentException ex)
        {
            return Result<UnitResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetByIdAsync(unitId, cancellationToken);
        if (unit is null)
            return Result<bool>.Failure("Unidade não encontrada.");

        unit.Deactivate();

        _repository.Update(unit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}