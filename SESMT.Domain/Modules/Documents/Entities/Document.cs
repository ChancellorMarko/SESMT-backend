using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Modules.Documents.Constants;

namespace SESMT.Domain.Modules.Documents.Entities;

public class Document : AggregateRootBase
{
    public Guid DocumentTypeId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Code { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public string Status { get; private set; }
    public string SecrecyLevel { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly? ValidFromDate { get; private set; }
    public DateOnly? ValidUntilDate { get; private set; }
    public string[] Keywords { get; private set; }
    public string Metadata { get; private set; }

    protected Document()
    {
        Code = null!;
        Title = null!;
        Status = null!;
        SecrecyLevel = null!;
        Keywords = Array.Empty<string>();
        Metadata = "{}";
    }

    public Document(
        Guid documentTypeId,
        Guid companyId,
        Guid? unitId,
        Guid? departmentId,
        string code,
        string title,
        string? description,
        string secrecyLevel,
        DateTime issueDate,
        DateTime? validFromDate,
        DateTime? validUntilDate,
        Guid ownerId,
        string[]? keywords = null,
        string? metadata = null)
    {
        ValidateDocumentTypeId(documentTypeId);
        ValidateCompanyId(companyId);
        ValidateCode(code);
        ValidateTitle(title);
        ValidateSecrecyLevel(secrecyLevel);
        ValidateOwnerId(ownerId);
        ValidateValidityDates(issueDate, validFromDate, validUntilDate);

        DocumentTypeId = documentTypeId;
        CompanyId = companyId;
        UnitId = unitId;
        DepartmentId = departmentId;
        Code = code.Trim();
        Title = title.Trim();
        Description = description?.Trim();
        Status = DocumentStatus.Draft;
        SecrecyLevel = secrecyLevel.Trim();
        IssueDate = issueDate;
        ValidFromDate = validFromDate;
        ValidUntilDate = validUntilDate;
        OwnerId = ownerId;
        Keywords = keywords?.Select(k => k.Trim()).ToArray() ?? Array.Empty<string>();
        Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata;
    }

    public void UpdateContent(
        string title,
        string? description,
        string[]? keywords,
        string? metadata)
    {
        EnsureEditable();

        ValidateTitle(title);

        Title = title.Trim();
        Description = description?.Trim();
        Keywords = keywords?.Select(k => k.Trim()).ToArray() ?? Array.Empty<string>();
        Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata;
        MarkAsUpdated();
    }

    public void UpdateValidity(DateTime issueDate, DateTime? validFromDate, DateTime? validUntilDate)
    {
        EnsureEditable();

        ValidateValidityDates(issueDate, validFromDate, validUntilDate);

        IssueDate = issueDate;
        ValidFromDate = validFromDate;
        ValidUntilDate = validUntilDate;
        MarkAsUpdated();
    }

    public void ChangeSecrecyLevel(string secrecyLevel)
    {
        ValidateSecrecyLevel(secrecyLevel);

        SecrecyLevel = secrecyLevel.Trim();
        MarkAsUpdated();
    }

    public void ChangeOwner(Guid newOwnerId)
    {
        ValidateOwnerId(newOwnerId);

        OwnerId = newOwnerId;
        MarkAsUpdated();
    }

    // ─── Transições de estado ───────────────────────────────────────────────
    public void SubmitForReview()
    {
        ValidateStatus(DocumentStatus.Draft);

        Status = DocumentStatus.InReview;
        MarkAsUpdated();
    }

    public void Approve()
    {
        ValidateStatus(DocumentStatus.InReview);

        Status = DocumentStatus.Approved;
        MarkAsUpdated();
    }

    public void Reject()
    {
        ValidateStatus(DocumentStatus.InReview);

        Status = DocumentStatus.Draft;
        MarkAsUpdated();
    }

    public void Publish(DateTime? validFromDate, DateTime? validUntilDate)
    {
        ValidateStatus(DocumentStatus.Approved);
        ValidateValidityDates(IssueDate, validFromDate, validUntilDate);

        Status = DocumentStatus.Active;
        ValidFromDate = validFromDate;
        ValidUntilDate = validUntilDate;
        MarkAsUpdated();
    }

    public void MarkAsExpired()
    {
        ValidateStatus(DocumentStatus.Active);

        Status = DocumentStatus.Expired;
        MarkAsUpdated();
    }

    public void MarkAsSuperseded()
    {
        ValidateStatus(new[] { DocumentStatus.Active, DocumentStatus.Expired });

        Status = DocumentStatus.Superseded;
        MarkAsUpdated();
    }

    public void Archive()
    {
        if (Status == DocumentStatus.Archived)
            throw new InvalidOperationException("O documento já está arquivado.");

        Status = DocumentStatus.Archived;
        MarkAsUpdated();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────
    public bool IsActive() => Status == DocumentStatus.Active;

    public bool IsExpiredBy(DateTime referenceDate)
        => ValidUntilDate.HasValue && ValidUntilDate.Value.Date < referenceDate.Date;

    public bool IsExpiringIn(DateTime referenceDate, int days)
    {
        if (!ValidUntilDate.HasValue)
            return false;

        var limit = referenceDate.Date.AddDays(days);
        return ValidUntilDate.Value.Date <= limit && ValidUntilDate.Value.Date >= referenceDate.Date;
    }

    private void EnsureEditable()
    {
        if (Status != DocumentStatus.Draft && Status != DocumentStatus.InReview)
            throw new InvalidOperationException(
                $"O documento não pode ser editado no status atual ({Status}). Apenas RASCUNHO e EM_REVISAO permitem edição.");
    }

    private void ValidateDocumentTypeId(Guid documentTypeId)
    {
        if (documentTypeId == Guid.Empty)
            throw new ArgumentException("O tipo do documento é obrigatório.");
    }

    private void ValidateCompanyId(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("A empresa do documento é obrigatória.");
    }

    private void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("O código do documento é obrigatório.");
    }

    private void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do documento é obrigatório.");
    }

    private void ValidateSecrecyLevel(string secrecyLevel)
    {
        if (string.IsNullOrWhiteSpace(secrecyLevel))
            throw new ArgumentException("A classificação de sigilo é obrigatória.");
    }

    private void ValidateOwnerId(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("O proprietário do documento é obrigatório.");
    }

    private void ValidateValidityDates(DateTime issueDate, DateTime? validFromDate, DateTime? validUntilDate)
    {
        if (validFromDate.HasValue && validUntilDate.HasValue && validUntilDate < validFromDate)
            throw new ArgumentException("A data fim da vigência não pode ser anterior à data de início.");

        if (validUntilDate.HasValue && validUntilDate < issueDate)
            throw new ArgumentException("A data fim da vigência não pode ser anterior à data de emissão.");
    }

    private void ValidateStatus(string requiredStatus)
    {
        if (Status != requiredStatus)
            throw new InvalidOperationException(
                $"Só é possível realizar a operação no status atual ({Status}). Status necessário: {requiredStatus}.");
    }

    private void ValidateStatus(IEnumerable<string> requiredStatuses)
    {
        if (!requiredStatuses.Contains(Status))
            throw new InvalidOperationException(
                $"Só é possível realizar a operação no status atual ({Status}). Statuses necessários: {string.Join(", ", requiredStatuses)}.");
    }
}