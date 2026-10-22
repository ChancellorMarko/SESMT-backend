using SESMT.Application.Common;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Application.Modules.Organization.Services;

public sealed class PositionService : IPositionService
{
    private readonly IPositionRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PositionService(
        IPositionRepository repository,
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PositionResponse>> CreateAsync(
        PositionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar que a empresa existe
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
            return Result<PositionResponse>.Failure("Empresa não encontrada.");

        // 2. Nome único por empresa
        if (await _repository.ExistsByNameAsync(request.CompanyId, request.Name, null, cancellationToken))
            return Result<PositionResponse>.Failure(
                "Já existe um cargo com esse nome nesta empresa.");

        try
        {
            var position = PositionMapper.ToEntity(request);

            await _repository.AddAsync(position, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<PositionResponse>.Success(PositionMapper.ToResponse(position));
        }
        catch (ArgumentException ex)
        {
            return Result<PositionResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<PositionResponse>> GetByIdAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var position = await _repository.GetByIdAsync(positionId, cancellationToken);

        return position is null
            ? Result<PositionResponse>.Failure("Cargo não encontrado.")
            : Result<PositionResponse>.Success(PositionMapper.ToResponse(position));
    }

    public async Task<Result<IEnumerable<PositionSummaryResponse>>> ListByCompanyAsync(
        Guid companyId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var positions = await _repository.ListByCompanyAsync(
            companyId, skip, pageSize, cancellationToken);

        var dtos = positions.Select(PositionMapper.ToSummary).ToList();

        return Result<IEnumerable<PositionSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<PositionResponse>> UpdateAsync(
        Guid positionId,
        PositionUpdateRequest updateData,
        CancellationToken cancellationToken = default)
    {
        var position = await _repository.GetByIdAsync(positionId, cancellationToken);
        if (position is null)
            return Result<PositionResponse>.Failure("Cargo não encontrado.");

        if (await _repository.ExistsByNameAsync(
                position.CompanyId, updateData.Name, positionId, cancellationToken))
        {
            return Result<PositionResponse>.Failure(
                "Já existe um cargo com esse nome nesta empresa.");
        }

        try
        {
            position.Update(
                name: updateData.Name,
                cbo: updateData.Cbo,
                description: updateData.Description);

            _repository.Update(position);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<PositionResponse>.Success(PositionMapper.ToResponse(position));
        }
        catch (ArgumentException ex)
        {
            return Result<PositionResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var position = await _repository.GetByIdAsync(positionId, cancellationToken);
        if (position is null)
            return Result<bool>.Failure("Cargo não encontrado.");

        position.Deactivate();
        _repository.Update(position);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
