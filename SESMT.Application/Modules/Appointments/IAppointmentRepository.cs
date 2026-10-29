using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.Appointments;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<Guid>> AccessibleCompaniesAsync(Guid actor, CancellationToken ct);
    Task<bool> HasRoleAsync(Guid actor, Guid company, string role, CancellationToken ct);
    Task<bool> ActiveLocationAsync(Guid location, Guid company, CancellationToken ct);
    Task<bool> ActiveUserAsync(Guid user, Guid company, CancellationToken ct);
    Task<bool> CanAttendAsync(Guid user, Guid company, Guid location, string[] services, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<AppointmentTeam?> GetTeamAsync(Guid team, Guid company, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<WorkPermitAppointment?> GetAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct);
    Task<AppointmentPage<WorkPermitAppointment>> ListAsync(IReadOnlyCollection<Guid> companies, DateTimeOffset? from,
        DateTimeOffset? to, string? status, string? search, int page, int pageSize, CancellationToken ct);
    Task<bool> HasPermitAsync(Guid appointment, CancellationToken ct);
    Task<(int Location, int Responsible)> OverlapsAsync(Guid except, Guid location, Guid responsible,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task AddAsync(WorkPermitAppointment appointment, CancellationToken ct);
    Task<AppointmentInvite?> FindInviteAsync(string hash, CancellationToken ct);
    Task<AppointmentInvite?> GetInviteAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct);
    Task AddInviteAsync(AppointmentInvite invite, CancellationToken ct);
    Task<int> PendingInviteRequestsAsync(Guid invite, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> SerializedAsync<T>(Func<Task<T>> action, CancellationToken ct);
}
