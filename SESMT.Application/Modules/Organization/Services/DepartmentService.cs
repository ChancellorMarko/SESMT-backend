using SESMT.Application.Common;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Application.Modules.Organization.Services;

public sealed class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repository;
    private readonly IUnitRepository _unitRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DepartmentService(
        IDepartmentRepository repository,
        IUnitRepository unitRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitRepository = unitRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentResponse>> CreateAsync(
        DepartmentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar que a unidade existe
        var unit = await _unitRepository.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null)
            return Result<DepartmentResponse>.Failure("Unidade não encontrada.");

        // 2. Nome único por unidade
        if (await _repository.ExistsByNameAsync(request.UnitId, request.Name, null, cancellationToken))
            return Result<DepartmentResponse>.Failure(
                "Já existe um departamento com esse nome nesta unidade.");

        try
        {
            var department = DepartmentMapper.ToEntity(request);

            await _repository.AddAsync(department, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<DepartmentResponse>.Success(DepartmentMapper.ToResponse(department));
        }
        catch (ArgumentException ex)
        {
            return Result<DepartmentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DepartmentResponse>> GetByIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var department = await _repository.GetByIdAsync(departmentId, cancellationToken);

        return department is null
            ? Result<DepartmentResponse>.Failure("Departamento não encontrado.")
            : Result<DepartmentResponse>.Success(DepartmentMapper.ToResponse(department));
    }

    public async Task<Result<IEnumerable<DepartmentSummaryResponse>>> ListByUnitAsync(
        Guid unitId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var departments = await _repository.ListByUnitAsync(
            unitId, skip, pageSize, cancellationToken);

        var dtos = departments.Select(DepartmentMapper.ToSummary).ToList();

        return Result<IEnumerable<DepartmentSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<DepartmentResponse>> UpdateAsync(
        Guid departmentId,
        DepartmentUpdateRequest updateData,
        CancellationToken cancellationToken = default)
    {
        var department = await _repository.GetByIdAsync(departmentId, cancellationToken);
        if (department is null)
            return Result<DepartmentResponse>.Failure("Departamento não encontrado.");

        // Nome único (ignorando o próprio registro)
        if (await _repository.ExistsByNameAsync(
                department.UnitId, updateData.Name, departmentId, cancellationToken))
        {
            return Result<DepartmentResponse>.Failure(
                "Já existe um departamento com esse nome nesta unidade.");
        }

        try
        {
            department.Update(
                name: updateData.Name,
                description: updateData.Description,
                responsibleId: updateData.ResponsibleId);

            _repository.Update(department);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<DepartmentResponse>.Success(DepartmentMapper.ToResponse(department));
        }
        catch (ArgumentException ex)
        {
            return Result<DepartmentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var department = await _repository.GetByIdAsync(departmentId, cancellationToken);
        if (department is null)
            return Result<bool>.Failure("Departamento não encontrado.");

        department.Deactivate();
        _repository.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
