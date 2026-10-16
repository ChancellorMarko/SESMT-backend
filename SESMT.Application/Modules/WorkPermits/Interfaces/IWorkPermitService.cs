using SESMT.Application.Modules.WorkPermits.DTOs;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IWorkPermitService
{
    Task<WorkPermitResponse> CreateAsync(CreateWorkPermitRequest request, Guid actorId, CancellationToken cancellationToken = default);
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
