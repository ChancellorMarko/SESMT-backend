using SESMT.Application.Common;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;
using SESMT.Application.Modules.Notifications;

namespace SESMT.Application.Modules.Documents.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentTypeRepository _typeRepository;
    private readonly IDocumentVersionRepository _versionRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationService? _notifications;

    public DocumentService(
        IDocumentRepository repository,
        IDocumentTypeRepository typeRepository,
        IDocumentVersionRepository versionRepository,
        IStoredFileRepository fileRepository,
        IFileStorage storage,
        IAuditLogRepository audit,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        NotificationService? notifications = null)
    {
        _repository = repository;
        _typeRepository = typeRepository;
        _versionRepository = versionRepository;
        _fileRepository = fileRepository;
        _storage = storage;
        _audit = audit;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<Result<DocumentResponse>> CreateAsync(
        DocumentCreateRequest request, CancellationToken ct = default)
    {
        var type = await _typeRepository.GetByIdAsync(request.DocumentTypeId, ct);
        if (type is null)
            return Result<DocumentResponse>.Failure("Tipo de documento não encontrado.");

        if (await _repository.ExistsByCodeAsync(
                request.DocumentTypeId, request.Code, request.UnitId, null, ct))
            return Result<DocumentResponse>.Failure(
                "Já existe um documento com esse código para esse tipo/unidade.");

        try
        {
            var entity = DocumentMapper.ToEntity(request);
            await _repository.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
        }
        catch (ArgumentException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity is null
            ? Result<DocumentResponse>.Failure("Documento não encontrado.")
            : Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
    }

    public async Task<Result<IEnumerable<DocumentSummaryResponse>>> ListAsync(
        Guid companyId, Guid? unitId, Guid? departmentId, Guid? documentTypeId,
        string? status, string? secrecyLevel, string? search,
        DateOnly? validUntilBefore, int pageSize, int pageNumber, CancellationToken ct = default)
    {
        if (pageSize <= 0) pageSize = 20;
        if (pageNumber <= 0) pageNumber = 1;

        var items = await _repository.ListAsync(
            companyId, unitId, departmentId, documentTypeId,
            status, secrecyLevel, search, validUntilBefore,
            (pageNumber - 1) * pageSize, pageSize, ct);

        return Result<IEnumerable<DocumentSummaryResponse>>.Success(
            items.Select(DocumentMapper.ToSummary).ToList());
    }

    public async Task<Result<DocumentResponse>> UpdateAsync(
        Guid id, DocumentUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<DocumentResponse>.Failure("Documento não encontrado.");

        try
        {
            entity.UpdateContent(request.Title, request.Description, request.Keywords, request.Metadata);
            entity.UpdateValidity(request.IssueDate, request.ValidFromDate, request.ValidUntilDate);
            entity.ChangeSecrecyLevel(request.SecrecyLevel);

            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
        }
        catch (ArgumentException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<bool>.Failure("Documento não encontrado.");

        entity.Archive();
        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<DocumentResponse>> SubmitForReviewAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<DocumentResponse>.Failure("Documento não encontrado.");

        try
        {
            entity.SubmitForReview();
            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentResponse>> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<DocumentResponse>.Failure("Documento não encontrado.");

        try
        {
            entity.Approve();
            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentResponse>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result<DocumentResponse>.Failure("Documento não encontrado.");

        try
        {
            entity.Publish(entity.ValidFromDate, entity.ValidUntilDate);
            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(entity));
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentVersionResponse>> AddVersionAsync(
        Guid documentId, DocumentVersionCreateRequest request,
        Stream fileContent, string fileName, string mimeType,
        CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result<DocumentVersionResponse>.Failure("Documento não encontrado.");

        // 1. Upload no storage
        var upload = await _storage.UploadAsync(fileContent, fileName, mimeType, ct);

        // 2. StoredFile (metadata do arquivo)
        var storedFile = new StoredFile(
            storageProvider: upload.Provider,
            bucket: upload.Bucket,
            objectKey: upload.ObjectKey,
            fileName: fileName,
            mimeType: mimeType,
            size: upload.SizeBytes,
            fileHash: upload.FileHash,
            isEncrypted: false,
            antivirusScanResult: "PENDENTE");

        await _fileRepository.AddAsync(storedFile, ct);

        // 3. Nova versão
        var nextVersionNumber = await _versionRepository.GetNextVersionNumberAsync(documentId, ct);

        var version = new DocumentVersion(
            documentId: documentId,
            versionNumber: nextVersionNumber,
            storedFileId: storedFile.Id,
            fileHash: upload.FileHash,
            authorId: request.AuthorId,
            justification: request.Justification);

        await _versionRepository.AddAsync(version, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<DocumentVersionResponse>.Success(DocumentVersionMapper.ToResponse(version));
    }

    public async Task<Result<IEnumerable<DocumentVersionResponse>>> ListVersionsAsync(
        Guid documentId, CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result<IEnumerable<DocumentVersionResponse>>.Failure("Documento não encontrado.");

        var versions = await _versionRepository.ListByDocumentAsync(documentId, ct);
        return Result<IEnumerable<DocumentVersionResponse>>.Success(
            versions.Select(DocumentVersionMapper.ToResponse).ToList());
    }

    public async Task<Result<DocumentVersionResponse>> ApproveVersionAsync(
        Guid documentId, Guid versionId, DocumentVersionApprovalRequest request,
        CancellationToken ct = default)
    {
        var version = await _versionRepository.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId)
            return Result<DocumentVersionResponse>.Failure("Versão não encontrada.");

        try
        {
            version.Approve(request.ApproverId);
            _versionRepository.Update(version);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentVersionResponse>.Success(DocumentVersionMapper.ToResponse(version));
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentVersionResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DocumentVersionResponse>> RejectVersionAsync(
        Guid documentId, Guid versionId, DocumentVersionRejectionRequest request,
        CancellationToken ct = default)
    {
        var version = await _versionRepository.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId)
            return Result<DocumentVersionResponse>.Failure("Versão não encontrada.");

        try
        {
            version.Reject(request.ApproverId, request.Justification);
            _versionRepository.Update(version);
            if (_notifications is not null)
                await _notifications.EnqueueDocumentReturnAsync(documentId, version.Id, version.AuthorId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentVersionResponse>.Success(DocumentVersionMapper.ToResponse(version));
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentVersionResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<FileDownloadResult>> DownloadVersionAsync(
        Guid documentId, Guid versionId, CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result<FileDownloadResult>.Failure("Documento não encontrado.");

        var version = await _versionRepository.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId)
            return Result<FileDownloadResult>.Failure("Versão não encontrada.");

        var storedFile = await _fileRepository.GetByIdAsync(version.StoredFileId, ct);
        if (storedFile is null)
            return Result<FileDownloadResult>.Failure("Arquivo não encontrado.");

        var stream = await _storage.DownloadAsync(storedFile.ObjectKey, ct);

        // ─── Audit: download (RF-061) ────────────────────────────────────
        var lastHash = await _audit.GetLastHashAsync(ct);
        await _audit.AddAsync(
            AuditLog.ForCustom(
                action: AuditAction.Download,
                resourceType: AuditResource.DocumentVersion,
                resourceId: versionId,
                actorId: _currentUser.UserId,
                actorEmail: _currentUser.Email,
                actorName: _currentUser.Name,
                previousData: null,
                newData: null,
                context: null,
                previousHash: lastHash,
                ipAddress: _currentUser.IpAddress,
                userAgent: _currentUser.UserAgent,
                companyId: document.CompanyId,
                resourceVersion: version.VersionNumber,
                result: "SUCESSO",
                traceId: _currentUser.TraceId),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<FileDownloadResult>.Success(new FileDownloadResult(
            Content: stream,
            FileName: storedFile.FileName,
            MimeType: storedFile.MimeType,
            SizeBytes: storedFile.Size,
            FileHash: storedFile.FileHash));
    }
}
