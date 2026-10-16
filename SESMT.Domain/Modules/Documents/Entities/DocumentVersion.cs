using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Modules.Documents.Constants;

namespace SESMT.Domain.Modules.Documents.Entities;

public class DocumentVersion : AggregateRootBase
{
    public Guid DocumentId { get; private set; }
    public Guid StoredFileId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ApproverId { get; private set; }
    public int VersionNumber { get; private set; }
    public string FileHash { get; private set; } = null!;
    public string? Justification { get; private set; }
    public string State { get; private set; } = null!;
    public bool IsCurrent { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    protected DocumentVersion()
    {
        FileHash = null!;
        State = null!;
    }

    public DocumentVersion(
        Guid documentId,
        int versionNumber,
        Guid storedFileId,
        string fileHash,
        Guid authorId,
        string? justification = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("O documento é obrigatório.");

        if (storedFileId == Guid.Empty)
            throw new ArgumentException("O arquivo da versão é obrigatório.");

        if (authorId == Guid.Empty)
            throw new ArgumentException("O autor da versão é obrigatório.");

        if (versionNumber <= 0)
            throw new ArgumentException("O número da versão deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("O hash do arquivo é obrigatório.");

        DocumentId = documentId;
        VersionNumber = versionNumber;
        StoredFileId = storedFileId;
        FileHash = fileHash.Trim();
        AuthorId = authorId;
        Justification = justification?.Trim();
        State = DocumentVersionState.Draft;
        IsCurrent = false;
    }

    public void ChangeFile(Guid storedFileId, string fileHash)
    {
        EnsureEditable();

        if (storedFileId == Guid.Empty)
            throw new ArgumentException("O arquivo da versão é obrigatório.");

        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("O hash do arquivo é obrigatório.");

        StoredFileId = storedFileId;
        FileHash = fileHash.Trim();
        MarkAsUpdated();
    }

    public void UpdateJustification(string? justification)
    {
        EnsureEditable();
        Justification = justification?.Trim();
        MarkAsUpdated();
    }

    public void SubmitForReview()
    {
        if (State != DocumentVersionState.Draft)
            throw new InvalidOperationException(
                $"Só é possível enviar para revisão uma versão em rascunho. Estado atual: {State}.");

        State = DocumentVersionState.InReview;
        MarkAsUpdated();
    }

    public void Approve(Guid approverId)
    {
        if (approverId == Guid.Empty)
            throw new ArgumentException("O aprovador é obrigatório.");

        if (State != DocumentVersionState.InReview)
            throw new InvalidOperationException(
                $"Só é possível aprovar uma versão em revisão. Estado atual: {State}.");

        State = DocumentVersionState.Approved;
        ApproverId = approverId;
        ApprovedAt = DateTimeOffset.UtcNow;
        MarkAsUpdated();
    }

    public void Reject(Guid approverId, string justification)
    {
        if (approverId == Guid.Empty)
            throw new ArgumentException("O aprovador é obrigatório.");

        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("A justificativa da rejeição é obrigatória.");

        if (State != DocumentVersionState.InReview)
            throw new InvalidOperationException(
                $"Só é possível rejeitar uma versão em revisão. Estado atual: {State}.");

        State = DocumentVersionState.Rejected;
        ApproverId = approverId;
        Justification = justification.Trim();
        MarkAsUpdated();
    }

    public void Publish()
    {
        if (State != DocumentVersionState.Approved)
            throw new InvalidOperationException(
                $"Só é possível publicar uma versão aprovada. Estado atual: {State}.");

        State = DocumentVersionState.Current;
        IsCurrent = true;
        MarkAsUpdated();
    }

    public void Supersede()
    {
        if (State != DocumentVersionState.Current)
            throw new InvalidOperationException(
                $"Só é possível substituir uma versão vigente. Estado atual: {State}.");

        State = DocumentVersionState.Superseded;
        IsCurrent = false;
        MarkAsUpdated();
    }

    public bool IsVigent() => IsCurrent && State == DocumentVersionState.Current;

    public bool IsApproved() => State == DocumentVersionState.Approved;

    private void EnsureEditable()
    {
        if (State != DocumentVersionState.Draft && State != DocumentVersionState.InReview)
            throw new InvalidOperationException(
                $"A versão não pode ser editada no estado atual ({State}). " +
                "Apenas RASCUNHO e EM_REVISAO permitem edição.");
    }
}
