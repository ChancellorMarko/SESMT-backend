using SESMT.Application.Modules.WorkPermits.DTOs;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IWorkPermitService
{
    IReadOnlyList<WorkPermitServiceResponse> GetServices();
    Task<IReadOnlyList<WorkPermitTemplateResponse>> ListTemplatesAsync(Guid actorId, Guid? companyId, Guid? templateId, CancellationToken cancellationToken = default);
    Task<WorkPermitTemplateResponse> CreateTemplateAsync(CreateWorkPermitTemplateRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitTemplateResponse> CreateTemplateVersionAsync(Guid templateId, CreateWorkPermitTemplateVersionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> SaveChecklistAsync(Guid permitId, SaveWorkPermitChecklistRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> CreateAsync(CreateWorkPermitRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitChecklistResponse> OpenAppointmentAsync(Guid appointmentId, OpenWorkPermitRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitChecklistResponse> GetChecklistAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitChecklistResponse> PatchChecklistAsync(Guid permitId, PatchWorkPermitChecklistRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitAppointmentResponse> CreateAppointmentAsync(CreateWorkPermitAppointmentRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitParticipantResponse> AddParticipantAsync(Guid permitId, AddWorkPermitParticipantRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitParticipantResponse> AcknowledgeAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task ConfigureRequirementAsync(CreateWorkPermitDocumentRequirementRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitDocumentRequirementResponse>> GetRequirementsAsync(Guid companyId, string serviceCode, Guid actorId, CancellationToken cancellationToken = default);
    Task LinkDocumentAsync(Guid permitId, LinkWorkPermitDocumentRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> GetAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitResponse>> ListAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkPermitTransitionResponse>> GetHistoryAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> ReleaseAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> StartAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> SuspendAsync(Guid permitId, Guid actorId, string reason, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> ResumeAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> CompleteAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WorkPermitResponse> CancelAsync(Guid permitId, Guid actorId, string reason, CancellationToken cancellationToken = default);
}
