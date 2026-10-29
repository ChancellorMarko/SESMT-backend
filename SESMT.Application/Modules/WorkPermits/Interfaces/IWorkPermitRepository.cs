using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Application.Modules.WorkPermits.DTOs;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IWorkPermitRepository
{
    Task AddAsync(WorkPermit permit, CancellationToken cancellationToken = default);
    Task AddAppointmentAsync(WorkPermitAppointment appointment, CancellationToken cancellationToken = default);
    Task<WorkPermitAppointment?> GetAppointmentAsync(Guid appointmentId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<bool> IsActiveDocumentTypeAsync(Guid documentTypeId, CancellationToken cancellationToken = default);
    Task<bool> HasWorkPermitForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveLocationInCompanyAsync(Guid locationId, Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveCompanyUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(WorkPermitParticipant participant, CancellationToken cancellationToken = default);
    Task<WorkPermitParticipant?> GetParticipantAsync(Guid permitId, Guid userId, CancellationToken cancellationToken = default);
    Task AddRequirementAsync(WorkPermitDocumentRequirement requirement, CancellationToken cancellationToken = default);
    Task AddTemplateVersionAsync(WorkPermitTemplateVersion template, CancellationToken cancellationToken = default);
    Task<WorkPermitTemplateVersion?> GetTemplateVersionAsync(Guid templateVersionId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitTemplateVersion>> ListTemplateVersionsAsync(IReadOnlyCollection<Guid> companyIds, Guid? templateId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitDocumentRequirement>> ListRequirementsAsync(Guid companyId, string serviceCode, CancellationToken cancellationToken = default);
    Task<bool> IsActiveCompanyDocumentAsync(Guid documentId, Guid companyId, CancellationToken cancellationToken = default);
    Task AddDocumentLinkAsync(DocumentLink documentLink, CancellationToken cancellationToken = default);
    Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default);
    async Task<WorkPermitReadinessAssessment> EvaluateReadinessAsync(WorkPermit permit, CancellationToken cancellationToken = default)
    {
        var ready = await IsReadyAsync(permit, cancellationToken);
        return new(ready, ready ? [] : [new("VERIFICACAO_PENDENTE")]);
    }
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    Task<IReadOnlyList<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasRoleInCompanyAsync(Guid userId, Guid companyId, string roleCode, CancellationToken cancellationToken = default);
    Task<WorkPermit?> GetByIdAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermit>> ListAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<WorkPermitSearchResult> SearchAsync(WorkPermitSearchRequest request, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitTransition>> ListHistoryAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
