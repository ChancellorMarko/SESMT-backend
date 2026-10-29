using System.Security.Cryptography;
using System.Text.Json;
using SESMT.Application.Common;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Application.Modules.Notifications;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Application.Modules.Documents.Services;

public sealed class DocumentService(
    IDocumentRepository repository,
    IDocumentTypeRepository typeRepository,
    IDocumentVersionRepository versionRepository,
    IStoredFileRepository fileRepository,
    IFileStorage storage,
    IAuditLogRepository audit,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    IDocumentAccessPolicy access,
    IDocumentSearchRepository searchRepository,
    IDocumentVersionWorkflow workflow,
    DocumentFileOptions fileOptions,
    NotificationService? notifications = null,
    DocumentQueryOptions? queryOptions = null) : IDocumentService
{
    private Guid Actor()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid actor || actor == Guid.Empty)
            throw new DocumentOperationException(401, "Sessão autenticada obrigatória.");
        return actor;
    }

    private async Task<Document> LoadAsync(Guid id, string action, CancellationToken ct)
    {
        var actor = Actor();
        var document = await repository.GetByIdAsync(id, ct);
        if (document is null || !document.Active || document.DeletedAt.HasValue || !await access.CanAccessAsync(actor, document, action, ct))
            throw new DocumentOperationException(404, "Documento não encontrado no alcance autorizado.");
        return document;
    }

    private static void Expected(Document document, int expected)
    {
        if (expected <= 0) throw new DocumentOperationException(400, "Informe a revisão esperada do documento.");
        if (document.Revision != expected) throw new DocumentOperationException(409, "O documento foi alterado. Recarregue a revisão atual.");
    }

    private static bool ValidSecrecy(string value) => value is SecrecyLevel.Public or SecrecyLevel.Internal or SecrecyLevel.Restricted or SecrecyLevel.Medical;

    private static void ValidateMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) throw new DocumentOperationException(400, "Os metadados devem ser um objeto JSON válido.");
        try
        {
            using var json = JsonDocument.Parse(metadata);
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        }
        catch (JsonException) { throw new DocumentOperationException(400, "Os metadados devem ser um objeto JSON válido."); }
    }

    public async Task<Result<DocumentResponse>> CreateAsync(DocumentCreateRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (!ValidSecrecy(request.SecrecyLevel) || request.Links is null || request.PersonId == Guid.Empty)
            throw new DocumentOperationException(400, "Classificação ou vínculos inválidos.");
        ValidateMetadata(request.Metadata);
        var type = await typeRepository.GetByIdAsync(request.DocumentTypeId, ct);
        if (type is null || !type.Active || type.DeletedAt.HasValue) throw new DocumentOperationException(404, "Tipo documental não encontrado.");
        if ((string.Equals(type.SecrecyLevel, SecrecyLevel.Medical, StringComparison.OrdinalIgnoreCase) && request.SecrecyLevel != SecrecyLevel.Medical)
            || (string.Equals(type.SecrecyLevel, SecrecyLevel.Restricted, StringComparison.OrdinalIgnoreCase) && request.SecrecyLevel is not (SecrecyLevel.Restricted or SecrecyLevel.Medical)))
            throw new DocumentOperationException(403, "A classificação do tipo documental não pode ser reduzida.");
        var document = DocumentMapper.ToEntity(request);
        var links = request.Links.ToList();
        if (request.PersonId.HasValue) links.Add(new DocumentLinkCreateRequest { EntityType = LinkedEntityType.Worker, EntityId = request.PersonId.Value });
        if (!await access.ValidateContextAsync(actor, document, links, ct))
            throw new DocumentOperationException(403, "Contexto, responsável ou vínculos fora do alcance autorizado.");
        if (await repository.ExistsByCodeAsync(request.DocumentTypeId, request.Code, request.UnitId, null, ct))
            throw new DocumentOperationException(409, "Já existe um documento com esse código no contexto.");
        await repository.AddAsync(document, ct);
        await access.AddLinksAsync(document.Id, links, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(document));
    }

    public async Task<Result<DocumentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Result<DocumentResponse>.Success(DocumentMapper.ToResponse(await LoadAsync(id, "ler", ct)));

    public async Task<Result<DocumentPageResponse>> SearchAsync(DocumentSearchRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > (queryOptions?.MaxPageSize ?? 100) || request.Page > int.MaxValue / request.PageSize
            || request.Search?.Length > 200 || request.From > request.To
            || new[] { request.CompanyId, request.UnitId, request.DepartmentId, request.DocumentTypeId, request.PersonId, request.TeamId }.Any(id => id == Guid.Empty)
            || request.Status is not null && !new[] { DocumentStatus.Draft, DocumentStatus.InReview, DocumentStatus.Approved, DocumentStatus.Active, DocumentStatus.Expired, DocumentStatus.Superseded, DocumentStatus.Archived }.Contains(request.Status)
            || request.SecrecyLevel is not null && request.SecrecyLevel is not (SecrecyLevel.Public or SecrecyLevel.Internal))
            throw new DocumentOperationException(400, "Filtros inválidos. A busca geral não inclui documentos restritos ou médicos.");
        var authorized = await access.ReadFilterAsync(actor, ct);
        return Result<DocumentPageResponse>.Success(await searchRepository.SearchAsync(request, authorized, ct));
    }

    public async Task<Result<IEnumerable<DocumentSummaryResponse>>> ListAsync(Guid companyId, Guid? unitId, Guid? departmentId, Guid? documentTypeId,
        string? status, string? secrecyLevel, string? search, DateOnly? validUntilBefore, int pageSize, int pageNumber, CancellationToken ct = default)
    {
        var result = await SearchAsync(new DocumentSearchRequest { CompanyId = companyId, UnitId = unitId, DepartmentId = departmentId,
            DocumentTypeId = documentTypeId, Status = status, SecrecyLevel = secrecyLevel, Search = search, ValidUntilBefore = validUntilBefore,
            Page = pageNumber, PageSize = pageSize }, ct);
        return Result<IEnumerable<DocumentSummaryResponse>>.Success(result.Value!.Items);
    }

    public async Task<Result<DocumentResponse>> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken ct = default)
    {
        var document = await LoadAsync(id, "editar", ct);
        if (!ValidSecrecy(request.SecrecyLevel)) throw new DocumentOperationException(400, "Classificação inválida.");
        ValidateMetadata(request.Metadata);
        // Raising secrecy requires the same explicit capability as restricted intake.
        if (request.SecrecyLevel is SecrecyLevel.Restricted or SecrecyLevel.Medical
            && request.SecrecyLevel != document.SecrecyLevel)
            throw new DocumentOperationException(422, "A classificação restrita deve ser definida na criação protegida do documento.");
        // A medical/restricted record cannot be downgraded to expose it through general search.
        if (document.SecrecyLevel is SecrecyLevel.Restricted or SecrecyLevel.Medical && request.SecrecyLevel != document.SecrecyLevel)
            throw new DocumentOperationException(403, "Não é permitido reduzir o sigilo do documento.");
        _ = new Document(document.DocumentTypeId, document.CompanyId, document.UnitId, document.DepartmentId, document.Code,
            request.Title, request.Description, request.SecrecyLevel, request.IssueDate, request.ValidFromDate, request.ValidUntilDate,
            document.OwnerId, request.Keywords, request.Metadata);
        document.UpdateContent(request.Title, request.Description, request.Keywords, request.Metadata);
        document.UpdateValidity(request.IssueDate, request.ValidFromDate, request.ValidUntilDate);
        document.ChangeSecrecyLevel(request.SecrecyLevel);
        document.TouchRevision(Actor());
        await unitOfWork.SaveChangesAsync(ct);
        return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(document));
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var document = await LoadAsync(id, "excluir", ct);
        document.Archive(); document.TouchRevision(Actor());
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<DocumentResponse>> SubmitForReviewAsync(Guid id, CancellationToken ct = default)
    {
        var document = await LoadAsync(id, "editar", ct);
        if (document.CurrentVersionNumber == 0) throw new DocumentOperationException(422, "Anexe uma evidência antes de solicitar conferência.");
        return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(document));
    }

    public Task<Result<DocumentResponse>> ApproveAsync(Guid id, CancellationToken ct = default) =>
        throw new DocumentOperationException(400, "A conferência exige versão e revisão esperadas. Use a decisão explícita de versão.");

    public async Task<Result<DocumentResponse>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var document = await LoadAsync(id, "editar", ct);
        var version = await versionRepository.GetCurrentByDocumentAsync(id, ct);
        if (version is null || version.VersionNumber != document.CurrentVersionNumber || version.State != DocumentVersionState.Current)
            throw new DocumentOperationException(422, "A evidência atual ainda não foi conferida e aceita.");
        return Result<DocumentResponse>.Success(DocumentMapper.ToResponse(document));
    }

    public async Task<Result<DocumentVersionResponse>> AddVersionAsync(Guid documentId, DocumentVersionCreateRequest request,
        Stream fileContent, string fileName, string mimeType, CancellationToken ct = default)
    {
        var actor = Actor();
        var document = await LoadAsync(documentId, "editar", ct);
        Expected(document, request.ExpectedVersion ?? 0);
        if (document.Status == DocumentStatus.Archived) throw new DocumentOperationException(422, "Documento arquivado.");
        var (content, safeName, detectedMime) = await ValidateFileAsync(fileContent, fileName, mimeType, ct);
        await using var validated = content;
        var hash = Convert.ToHexString(SHA256.HashData(validated.ToArray())).ToLowerInvariant();
        FileStorageResult? upload = null;
        try
        {
            upload = await storage.UploadAsync(validated, safeName, detectedMime, ct);
            if (upload.SizeBytes != validated.Length || !string.Equals(upload.FileHash, hash, StringComparison.OrdinalIgnoreCase))
                throw new DocumentOperationException(422, "O armazenamento não confirmou a integridade do arquivo.");
            // Recheck capabilities after a potentially slow upload.
            if (!await access.CanAccessAsync(actor, document, "editar", ct))
                throw new DocumentOperationException(403, "A autorização para anexar foi revogada.");
            var storedFile = new StoredFile(upload.Provider, upload.Bucket, upload.ObjectKey, safeName, detectedMime, upload.SizeBytes, hash, false, "PENDENTE");
            var number = await versionRepository.GetNextVersionNumberAsync(documentId, ct);
            var version = new DocumentVersion(documentId, number, storedFile.Id, hash, actor, request.Justification);
            version.SubmitForReview();
            document.RegisterVersion(number, actor);
            await fileRepository.AddAsync(storedFile, ct);
            await versionRepository.AddAsync(version, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result<DocumentVersionResponse>.Success(DocumentVersionMapper.ToResponse(version));
        }
        catch
        {
            if (upload is not null)
            {
                try { await storage.DeleteAsync(upload.ObjectKey, CancellationToken.None); }
                catch { /* An unreferenced object is quarantined; no available evidence was committed. */ }
            }
            throw;
        }
    }

    private async Task<(MemoryStream Content, string Name, string Mime)> ValidateFileAsync(Stream input, string fileName, string mimeType, CancellationToken ct)
    {
        if (fileOptions.MaxSizeBytes is <= 0 or > 50 * 1024 * 1024)
            throw new InvalidOperationException("Limite de arquivo deve estar entre 1 byte e 50 MiB.");
        var result = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            int size;
            while ((size = await input.ReadAsync(chunk, ct)) > 0)
            {
                if (result.Length + size > fileOptions.MaxSizeBytes) throw new DocumentOperationException(413, "O arquivo excede o limite configurado.");
                await result.WriteAsync(chunk.AsMemory(0, size), ct);
            }
            var bytes = result.ToArray();
            var detected = bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf"
                : bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
                : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg" : null;
            var name = fileName.Replace('\\', '/').Split('/').Last();
            var extension = Path.GetExtension(name).ToLowerInvariant();
            var matchingExtension = detected switch { "application/pdf" => extension == ".pdf", "image/png" => extension == ".png", "image/jpeg" => extension is ".jpg" or ".jpeg", _ => false };
            if (detected is null || !matchingExtension || !string.Equals(detected, mimeType, StringComparison.OrdinalIgnoreCase)
                || !fileOptions.AllowedMimeTypes.Contains(detected, StringComparer.OrdinalIgnoreCase)
                || name.Length > 255 || name.Any(char.IsControl))
                throw new DocumentOperationException(400, "Tipo, conteúdo ou nome de arquivo inválido.");
            result.Position = 0;
            return (result, name, detected);
        }
        catch { await result.DisposeAsync(); throw; }
    }

    public async Task<Result<IEnumerable<DocumentVersionResponse>>> ListVersionsAsync(Guid documentId, CancellationToken ct = default)
    {
        await LoadAsync(documentId, "ler", ct);
        var versions = await versionRepository.ListByDocumentAsync(documentId, ct);
        return Result<IEnumerable<DocumentVersionResponse>>.Success(versions.Select(DocumentVersionMapper.ToResponse).ToArray());
    }

    public async Task<Result<DocumentVersionResponse>> ReviewAsync(Guid documentId, Guid versionId, DocumentReviewRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        var document = await LoadAsync(documentId, "ler", ct);
        Expected(document, request.ExpectedVersion);
        if (request.Decision is not ("aceita" or "reprovada") || request.Version <= 0
            || request.Reason?.Length > 2000 || request.Decision == "reprovada" && string.IsNullOrWhiteSpace(request.Reason))
            throw new DocumentOperationException(400, "Decisão, versão e motivo da reprovação devem ser válidos.");
        var version = await versionRepository.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId) throw new DocumentOperationException(404, "Versão não encontrada.");
        if (version.VersionNumber != request.Version || version.VersionNumber != document.CurrentVersionNumber || version.State != DocumentVersionState.InReview)
            throw new DocumentOperationException(409, "A versão atual foi alterada ou já recebeu decisão.");
        if (!await access.CanReviewAsync(actor, document, ct))
            throw new DocumentOperationException(403, "Conferente não designado ou sem concessão específica.");
        if (request.Decision == "aceita")
        {
            var file = await fileRepository.GetByIdAsync(version.StoredFileId, ct);
            if (file is null || !file.Active || file.DeletedAt.HasValue || file.Size <= 0 || file.Size > fileOptions.MaxSizeBytes)
                throw new DocumentOperationException(422, "O arquivo da evidência não está disponível para conferência.");
            try
            {
                await using var stream = await storage.DownloadAsync(file.ObjectKey, ct);
                var checkedFile = await ValidateFileAsync(stream, file.FileName, file.MimeType, ct);
                await using var content = checkedFile.Content;
                var hash = Convert.ToHexString(SHA256.HashData(content.ToArray())).ToLowerInvariant();
                if (content.Length != file.Size || hash != file.FileHash || hash != version.FileHash)
                    throw new DocumentOperationException(422, "A integridade da evidência não foi confirmada.");
            }
            catch (FileNotFoundException) { throw new DocumentOperationException(422, "O arquivo da evidência não está disponível para conferência."); }
            if (!await access.CanReviewAsync(actor, document, ct)) throw new DocumentOperationException(403, "A autorização para conferir foi revogada.");
        }
        if (request.Decision == "reprovada" && notifications is not null)
            await notifications.EnqueueDocumentReturnAsync(documentId, version.Id, version.AuthorId, ct);
        await workflow.ReviewAsync(document, version, actor, request.Decision == "aceita", request.Reason, ct);
        return Result<DocumentVersionResponse>.Success(DocumentVersionMapper.ToResponse(version));
    }

    public async Task<Result<DocumentVersionResponse>> ApproveVersionAsync(Guid documentId, Guid versionId, DocumentVersionApprovalRequest request, CancellationToken ct = default)
    {
        await LoadAsync(documentId, "ler", ct);
        var version = await versionRepository.GetByIdAsync(versionId, ct);
        // Authorization is always checked by ReviewAsync before returning any content.
        return await ReviewAsync(documentId, versionId, new(request.ExpectedVersion, version?.VersionNumber ?? 1, "aceita", null), ct);
    }
    public async Task<Result<DocumentVersionResponse>> RejectVersionAsync(Guid documentId, Guid versionId, DocumentVersionRejectionRequest request, CancellationToken ct = default)
    {
        await LoadAsync(documentId, "ler", ct);
        var version = await versionRepository.GetByIdAsync(versionId, ct);
        return await ReviewAsync(documentId, versionId, new(request.ExpectedVersion, version?.VersionNumber ?? 1, "reprovada", request.Justification), ct);
    }

    public async Task<Result<FileDownloadResult>> DownloadVersionAsync(Guid documentId, Guid versionId, CancellationToken ct = default)
    {
        var document = await LoadAsync(documentId, "ler", ct);
        var version = await versionRepository.GetByIdAsync(versionId, ct);
        if (version is null || !version.Active || version.DeletedAt.HasValue || version.DocumentId != documentId) throw new DocumentOperationException(404, "Versão não encontrada.");
        var file = await fileRepository.GetByIdAsync(version.StoredFileId, ct);
        if (file is null || !file.Active || file.DeletedAt.HasValue || file.Size <= 0 || file.Size > fileOptions.MaxSizeBytes)
            throw new DocumentOperationException(404, "Arquivo não disponível.");
        MemoryStream? content = null;
        try
        {
            await using var stream = await storage.DownloadAsync(file.ObjectKey, ct);
            var validated = await ValidateFileAsync(stream, file.FileName, file.MimeType, ct);
            content = validated.Content;
            var actualHash = Convert.ToHexString(SHA256.HashData(content.ToArray())).ToLowerInvariant();
            if (content.Length != file.Size || actualHash != file.FileHash || actualHash != version.FileHash)
                throw new DocumentOperationException(409, "Integridade do arquivo não confirmada.");
            if (!await access.CanAccessAsync(Actor(), document, "ler", ct)) throw new DocumentOperationException(403, "A autorização para acessar o arquivo foi revogada.");
            var lastHash = await audit.GetLastHashAsync(ct);
            await audit.AddAsync(AuditLog.ForCustom(AuditAction.Download, AuditResource.DocumentVersion, versionId, Actor(),
                currentUser.Email, currentUser.Name, null, null, null, lastHash, currentUser.IpAddress, currentUser.UserAgent,
                companyId: document.CompanyId, resourceVersion: version.VersionNumber, result: "SUCESSO", traceId: currentUser.TraceId), ct);
            await unitOfWork.SaveChangesAsync(ct);
            content.Position = 0;
            return Result<FileDownloadResult>.Success(new(content, file.FileName, file.MimeType, file.Size, file.FileHash));
        }
        catch (FileNotFoundException) { if (content is not null) await content.DisposeAsync(); throw new DocumentOperationException(404, "Arquivo não disponível."); }
        catch { if (content is not null) await content.DisposeAsync(); throw; }
    }

    public async Task<Result<FileDownloadResult>> DownloadFileAsync(Guid documentId, Guid fileId, CancellationToken ct = default)
    {
        await LoadAsync(documentId, "ler", ct);
        var version = (await versionRepository.ListByDocumentAsync(documentId, ct)).FirstOrDefault(v => v.StoredFileId == fileId);
        if (version is null) throw new DocumentOperationException(404, "Arquivo não encontrado.");
        return await DownloadVersionAsync(documentId, version.Id, ct);
    }

    public async Task<Result<DocumentGrantResponse>> GrantAsync(Guid documentId, DocumentGrantRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        var document = await repository.GetByIdAsync(documentId, ct);
        if (document is null || !document.Active || document.DeletedAt.HasValue || !await access.CanGrantAsync(actor, document, ct))
            throw new DocumentOperationException(404, "Documento não encontrado no alcance autorizado para concessão.");
        Expected(document, request.ExpectedVersion);
        if (!await access.CanReceiveGrantAsync(request.UserId, document, ct))
            throw new DocumentOperationException(403, "Destinatário sem escopo ou capacidade específica.");
        var grant = new DocumentAccessGrant(documentId, request.UserId, actor, request.CanReview, request.ExpiresAt);
        await access.AddGrantAsync(grant, ct);
        document.TouchRevision(actor);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<DocumentGrantResponse>.Success(new(grant.Id, documentId, grant.UserId, grant.CanReview, grant.ExpiresAt, grant.Version));
    }

    public async Task RevokeGrantAsync(Guid documentId, Guid grantId, int expectedVersion, CancellationToken ct = default)
    {
        var actor = Actor();
        var document = await repository.GetByIdAsync(documentId, ct);
        if (document is null || !await access.CanGrantAsync(actor, document, ct)) throw new DocumentOperationException(404, "Documento não encontrado.");
        var grant = await access.GetGrantAsync(grantId, documentId, ct);
        if (grant is null) throw new DocumentOperationException(404, "Concessão não encontrada.");
        if (grant.Version != expectedVersion || grant.RevokedAt.HasValue) throw new DocumentOperationException(409, "A concessão foi alterada.");
        grant.Revoke(actor, expectedVersion); document.TouchRevision(actor);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
