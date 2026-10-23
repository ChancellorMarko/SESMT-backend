using SESMT.Application.Common;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Application.Modules.Documents.Services;

public sealed class DocumentTypeService : IDocumentTypeService
{
    private readonly IDocumentTypeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DocumentTypeService(IDocumentTypeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DocumentTypeResponse>> CreateAsync(
        DocumentTypeCreateRequest request, CancellationToken ct = default)
    {
        if (await _repository.ExistsByCodeAsync(request.Code, null, ct))
            return Result<DocumentTypeResponse>.Failure(
                "Já existe um tipo de documento com esse código.");

        try
        {
            var entity = DocumentTypeMapper.ToEntity(request);
            await _repository.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentTypeResponse>.Success(DocumentTypeMapper.ToResponse(entity));
        }
        catch (ArgumentException ex)
        {
            return Result<DocumentTypeResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentTypeResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity is null
            ? Result<DocumentTypeResponse>.Failure("Tipo de documento não encontrado.")
            : Result<DocumentTypeResponse>.Success(DocumentTypeMapper.ToResponse(entity));
    }

    public async Task<Result<IEnumerable<DocumentTypeSummaryResponse>>> ListAsync(
        string? category, bool? onlyActive, CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(category, onlyActive, ct);
        return Result<IEnumerable<DocumentTypeSummaryResponse>>.Success(
            items.Select(DocumentTypeMapper.ToSummary).ToList());
    }

    public async Task<Result<DocumentTypeResponse>> UpdateAsync(
        Guid id, DocumentTypeUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<DocumentTypeResponse>.Failure("Tipo de documento não encontrado.");

        try
        {
            entity.UpdateDocumentType(
                entity.Code, request.Name, request.Description, request.Category,
                request.SecrecyLevel, request.RequiresSignature, request.RequiresApproval,
                request.PeriodicityInDays, request.RetentionPeriodInYears,
                request.MetadataSchema, request.SchemaVersion);

            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentTypeResponse>.Success(DocumentTypeMapper.ToResponse(entity));
        }
        catch (ArgumentException ex)
        {
            return Result<DocumentTypeResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<bool>.Failure("Tipo de documento não encontrado.");

        entity.Deactivate();
        entity.MarkAsUpdated();
        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
