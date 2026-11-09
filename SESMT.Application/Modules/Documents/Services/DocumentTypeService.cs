using System.Text.Json.Nodes;
using SESMT.Application.Common;
using SESMT.Application.Exceptions;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Application.Modules.Documents.Services;

public sealed class DocumentTypeService : IDocumentTypeService
{
    public const string VersionConflictError = "O tipo documental foi alterado. Recarregue antes de salvar.";
    public const string ImmutablePolicyError =
        "A política documental (sigilo, assinatura, aprovação, periodicidade, guarda e metadados) não pode ser alterada. Cadastre um novo tipo para uma política diferente.";

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
        Guid actorId, DocumentTypeCreateRequest request, CancellationToken ct = default)
    {
        await RequireManagerAsync(actorId, ct);
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
        Guid actorId, Guid id, DocumentTypeUpdateRequest request, CancellationToken ct = default)
    {
        await RequireManagerAsync(actorId, ct);
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null || entity.DeletedAt is not null)
            return Result<DocumentTypeResponse>.Failure("Tipo de documento não encontrado.");

        if (entity.Version != request.ExpectedVersion)
            return Result<DocumentTypeResponse>.Failure(VersionConflictError);

        if (!KeepsPolicy(entity, request))
            return Result<DocumentTypeResponse>.Failure(ImmutablePolicyError);

        try
        {
            entity.Rename(request.Name, request.Description, request.Category);
            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentTypeResponse>.Success(DocumentTypeMapper.ToResponse(entity));
        }
        catch (ArgumentException ex)
        {
            return Result<DocumentTypeResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(Guid actorId, Guid id, CancellationToken ct = default)
    {
        await RequireManagerAsync(actorId, ct);
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null || entity.DeletedAt is not null)
            return Result<bool>.Failure("Tipo de documento não encontrado.");

        entity.Deactivate();
        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private async Task RequireManagerAsync(Guid actorId, CancellationToken ct)
    {
        if (!await _repository.UserCanManageAsync(actorId, ct))
            throw new ResourceForbiddenException();
    }

    private static bool KeepsPolicy(DocumentType entity, DocumentTypeUpdateRequest request) =>
        string.Equals(entity.SecrecyLevel, request.SecrecyLevel, StringComparison.Ordinal)
        && entity.RequiresSignature == request.RequiresSignature
        && entity.RequiresApproval == request.RequiresApproval
        && entity.PeriodicityInDays == request.PeriodicityInDays
        && entity.RetentionPeriodInYears == request.RetentionPeriodInYears
        && entity.SchemaVersion == request.SchemaVersion
        && SameJson(entity.MetadataSchema, request.MetadataSchema);

    // jsonb normalizes formatting, so the schema is compared by content, not by text.
    private static bool SameJson(string stored, string? received)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(stored), JsonNode.Parse(received ?? "")); }
        catch (System.Text.Json.JsonException) { return false; }
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

        // AuditInterceptor records every create, rename and deactivation of a type.
        var logs = await _audit.QueryAsync(
            actorId: null, action: null, resourceType: AuditResource.DocumentType, resourceId: id,
            from: null, to: null,
            companyIds: Array.Empty<Guid>(),      // eventos sem empresa
            skip: 0, take: 200, ct);

        var entries = logs
            .Select(l => new DocumentTypeHistoryEntryResponse(
                l.EventId, l.Action, l.ActorName, l.OccurredAt, l.ResourceVersion ?? 0))
            .ToArray();
        return Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>.Success(entries);
    }
}
