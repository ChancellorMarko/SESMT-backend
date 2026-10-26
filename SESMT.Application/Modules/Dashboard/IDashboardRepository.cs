namespace SESMT.Application.Modules.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardData> GetDataAsync(Guid actorId, CancellationToken cancellationToken, DashboardFilter? activityFilter = null);
}

public sealed record DashboardData(IReadOnlyList<DashboardAppointment> Appointments,
    IReadOnlyList<DashboardDocumentPendency> DocumentPendencies,
    IReadOnlyList<DashboardReviewPendency> ReviewPendencies,
    IReadOnlySet<Guid> AllowedLocalIds, IReadOnlySet<Guid> AllowedTeamIds, IReadOnlySet<Guid> AllowedServiceIds,
    IReadOnlyList<DashboardActivityTransition> ActivityTransitions);
