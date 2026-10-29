namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed record WorkPermitReadinessIssue(string Code, Guid? ParticipantUserId = null, Guid? DocumentTypeId = null, string? ServiceCode = null);
public sealed record WorkPermitReadinessAssessment(bool Ready, IReadOnlyList<WorkPermitReadinessIssue> Issues);
public sealed record WorkPermitEvidence(Guid ParticipantUserId, Guid DocumentTypeId, bool ReviewedCurrentVersion,
    bool FileAvailable, DateOnly? ValidFrom, DateOnly? ValidUntil, string? ReviewState = null);

/// <summary>Administrative requirements only; it never determines clinical fitness.</summary>
public static class WorkPermitReadinessPolicy
{
    public static WorkPermitReadinessAssessment Evaluate(WorkPermit permit, bool appointmentConfirmed,
        bool checklistReady, IReadOnlyList<WorkPermitParticipant> participants, IReadOnlySet<Guid> activeCompanyUsers,
        IReadOnlyList<WorkPermitDocumentRequirement> requirements, IReadOnlyList<WorkPermitEvidence> evidence,
        DateOnly validFrom, DateOnly validUntil, string? expectedDeclaration = null)
    {
        var issues = new List<WorkPermitReadinessIssue>();
        if (!appointmentConfirmed) issues.Add(new("AGENDAMENTO_NAO_CONFIRMADO"));
        if (!checklistReady) issues.Add(new("CHECKLIST_PENDENTE"));
        if (participants.Count == 0) issues.Add(new("PARTICIPANTES_AUSENTES"));
        var codes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var code in codes)
            if (!requirements.Any(requirement => requirement.ServiceCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
                issues.Add(new("MATRIZ_AUSENTE", ServiceCode: code));
        if (codes.Length == 0) issues.Add(new("SERVICOS_AUSENTES"));

        var applicable = requirements.Where(requirement => codes.Contains(requirement.ServiceCode, StringComparer.OrdinalIgnoreCase))
            .Select(requirement => requirement.DocumentTypeId).Distinct().ToArray();
        foreach (var participant in participants)
        {
            if (!activeCompanyUsers.Contains(participant.UserId))
                issues.Add(new("PARTICIPANTE_FORA_DO_CONTEXTO", participant.UserId));
            if (!participant.HasAcknowledged(permit.ContentVersion) || expectedDeclaration is not null
                && participant.Acknowledgements.LastOrDefault()?.Declaration != expectedDeclaration)
                issues.Add(new("CIENCIA_PENDENTE", participant.UserId));
            foreach (var documentType in applicable)
            {
                var matches = evidence.Where(item => item.ParticipantUserId == participant.UserId && item.DocumentTypeId == documentType).ToArray();
                var approved = matches.Where(item => item.ReviewedCurrentVersion && item.FileAvailable).ToArray();
                if (approved.Any(item => (!item.ValidFrom.HasValue || item.ValidFrom <= validFrom)
                    && (!item.ValidUntil.HasValue || item.ValidUntil >= validUntil))) continue;
                var reason = matches.Length == 0 ? "DOCUMENTO_AUSENTE"
                    : matches.Any(item => item.ReviewState == Documents.Constants.DocumentVersionState.Rejected) ? "DOCUMENTO_REPROVADO"
                    : matches.Any(item => !item.ReviewedCurrentVersion) ? "AGUARDANDO_CONFERENCIA"
                    : approved.Length == 0 ? "ARQUIVO_INDISPONIVEL" : "DOCUMENTO_FORA_DA_VALIDADE";
                issues.Add(new(reason, participant.UserId, documentType));
            }
        }
        return new(issues.Count == 0, issues);
    }
}
