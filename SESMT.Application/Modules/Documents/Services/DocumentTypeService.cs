using SESMT.Application.Common;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Application.Modules.Documents.Services;

public sealed class DocumentTypeService : IDocumentTypeService
{
    private readonly IDocumentTypeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogRepository? _audit;

    public DocumentTypeService(
        IDocumentTypeRepository repository,
        IUnitOfWork unitOfWork,
        IAuditLogRepository? audit = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
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
        return entity is null || entity.DeletedAt is not null
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
        if (entity is null || entity.DeletedAt is not null)
            return Result<DocumentTypeResponse>.Failure("Tipo de documento não encontrado.");

        // ← NOVO: concorrência otimista
        if (entity.Version != request.ExpectedVersion)
            return Result<DocumentTypeResponse>.Failure(
                "O tipo documental foi alterado. Recarregue antes de salvar.");

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
        if (entity is null || entity.DeletedAt is not null)
            return Result<bool>.Failure("Tipo de documento não encontrado.");

        entity.Deactivate();
        _repository.Update(entity);                            // (removido MarkAsUpdated duplicado — Deactivate já chama)
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    // ─── NOVOS ──────────────────────────────────────────────────────────

    public async Task<Result<DocumentTypeManagementContextResponse>> GetManagementContextAsync(
        Guid userId, CancellationToken ct = default)
    {
        var canManage = await _repository.UserCanManageAsync(userId, ct);
        return Result<DocumentTypeManagementContextResponse>.Success(
            new DocumentTypeManagementContextResponse(canManage));
    }

    public async Task<Result<DocumentTypePageResponse>> ListPagedAsync(
        string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _repository.ListPagedAsync(q, page, pageSize, ct);
        var mapped = items
            .Select(entity => new DocumentTypeListItemResponse(
                entity.Id, DocumentTypeMapper.ToResponse(entity), entity.Version))
            .ToArray();
        return Result<DocumentTypePageResponse>.Success(
            new DocumentTypePageResponse(mapped, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), total));
    }

    public async Task<Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>> GetHistoryAsync(
        Guid id, CancellationToken ct = default)
    {
        var exists = await _repository.GetByIdAsync(id, ct);
        if (exists is null || exists.DeletedAt is not null)
            return Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>.Failure(
                "Tipo de documento não encontrado.");

        if (_audit is null) return Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>.Success(Array.Empty<DocumentTypeHistoryEntryResponse>());

        // Enquanto ninguém escreve AuditLog com ResourceType="document_type" no Create/Update/Deactivate,
        // isto retorna [] e o front mostra "Nenhuma alteração registrada neste fluxo."
        var logs = await _audit.QueryAsync(
            actorId: null, action: null, resourceType: "document_type", resourceId: id,
            from: null, to: null,
            companyIds: Array.Empty<Guid>(),      // eventos sem empresa
            skip: 0, take: 200, ct);

        var entries = logs
            .Select(l => new DocumentTypeHistoryEntryResponse(l.EventId, l.Action, l.ActorName, l.OccurredAt))
            .ToArray();
        return Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>.Success(entries);
    }
}
