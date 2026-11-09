using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.OccupationalHealth.Entities;

/// <summary>Registry of an externally issued ASO. Clinical notes and diagnoses are not collected.</summary>
public sealed class OccupationalCertificate : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid WorkerId { get; private set; }
    public Guid CommandId { get; private set; }
    public string ExamKind { get; private set; } = "";
    public DateOnly ExamOn { get; private set; }
    public DateOnly IssuedOn { get; private set; }
    public DateOnly? NextEvaluationOn { get; private set; }
    public string ProtectedDetails { get; private set; } = "";
    public int Revision { get; private set; } = 1;
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationCode { get; private set; }
    private OccupationalCertificate() { }
    public OccupationalCertificate(Guid company, Guid unit, Guid worker, Guid command, Guid actor)
    {
        if (new[] { company, unit, worker, command, actor }.Contains(Guid.Empty)) throw new ArgumentException("Contexto obrigatório para o ASO.");
        CompanyId = company; UnitId = unit; WorkerId = worker; CommandId = command; CreatedBy = actor;
    }
    public void Set(string kind, DateOnly exam, DateOnly issued, DateOnly? next, string protectedDetails)
    {
        if (RevokedAt.HasValue) throw new InvalidOperationException("O registro foi invalidado.");
        if (kind is not ("ADMISSION" or "PERIODIC" or "RETURN_TO_WORK" or "RISK_CHANGE" or "DISMISSAL") || exam == default
            || issued < exam || next < exam || string.IsNullOrWhiteSpace(protectedDetails)) throw new ArgumentException("Datas ou tipo de exame inválidos.");
        ExamKind = kind; ExamOn = exam; IssuedOn = issued; NextEvaluationOn = next; ProtectedDetails = protectedDetails;
    }
    public void RecordCorrection(Guid actor) { Revision++; MarkAsUpdated(actor); }
    public void Revoke(string code, Guid actor)
    {
        if (RevokedAt.HasValue || code is not ("ENTRY_ERROR" or "DUPLICATE" or "REPLACED")) throw new ArgumentException("Motivo administrativo inválido.");
        RevocationCode = code; RevokedAt = DateTimeOffset.UtcNow; RecordCorrection(actor);
    }
}
