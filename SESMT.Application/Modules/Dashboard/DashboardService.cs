using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Application.Modules.WorkPermits.Services;

namespace SESMT.Application.Modules.Dashboard;

public sealed class DashboardService(IDashboardRepository repository)
{
    public async Task<DashboardSummary> GetSummaryAsync(Guid actorId, DashboardFilter filter, CancellationToken ct)
    {
        var data = await GetFilteredAsync(actorId, filter, ct);
        return new(data.Appointments.Count(x => x.OperationalStatus == "EM_VERIFICACAO"),
            data.Appointments.Count(x => x.OperationalStatus == "LIBERADA"),
            data.Appointments.Count(x => x.OperationalStatus == "EM_EXECUCAO"),
            data.Reviews.Count, data.DocumentPendencies.Sum(x => x.PendingRequirements),
            new(filter.From, filter.To));
    }

    public async Task<IReadOnlyList<DashboardAppointment>> ListAppointmentsAsync(Guid actorId, DashboardFilter filter, CancellationToken ct) =>
        (await GetFilteredAsync(actorId, filter, ct)).Appointments;

    public async Task<IReadOnlyList<DashboardDocumentPendency>> ListDocumentPendenciesAsync(Guid actorId, DashboardFilter filter, CancellationToken ct) =>
        (await GetFilteredAsync(actorId, filter, ct)).DocumentPendencies;

    public async Task<IReadOnlyList<DashboardReviewPendency>> ListReviewPendenciesAsync(Guid actorId, DashboardFilter filter, CancellationToken ct) =>
        (await GetFilteredAsync(actorId, filter, ct)).Reviews;

    private async Task<FilteredData> GetFilteredAsync(Guid actorId, DashboardFilter filter, CancellationToken ct)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            throw new ArgumentException("O início do período deve ser anterior ao fim.");
        if (filter.From?.Offset != TimeSpan.Zero && filter.From.HasValue || filter.To?.Offset != TimeSpan.Zero && filter.To.HasValue)
            throw new ArgumentException("from e to devem estar em UTC.");
        if (filter.LocalId == Guid.Empty || filter.TeamId == Guid.Empty || filter.ServiceId == Guid.Empty)
            throw new ArgumentException("Os filtros de identificador devem conter UUIDs válidos.");
        if (filter.OperationalStatus is not null && !new[] { "EM_VERIFICACAO", "LIBERADA", "EM_EXECUCAO", "SUSPENSA", "CONCLUIDA", "CANCELADA" }.Contains(filter.OperationalStatus))
            throw new ArgumentException("statusOperacional inválido.");
        if (filter.ReviewStatus is not null && !new[] { "AGUARDANDO_REVISAO", "DEVOLVIDA", "APROVADA" }.Contains(filter.ReviewStatus))
            throw new ArgumentException("statusRevisao inválido.");

        var data = await repository.GetDataAsync(actorId, ct);
        if ((filter.LocalId.HasValue && !data.AllowedLocalIds.Contains(filter.LocalId.Value))
            || (filter.TeamId.HasValue && !data.AllowedTeamIds.Contains(filter.TeamId.Value))
            || (filter.ServiceId.HasValue && !data.AllowedServiceIds.Contains(filter.ServiceId.Value)))
            throw new WorkPermitForbiddenException();
        var appointments = data.Appointments.Where(x => (!filter.From.HasValue || x.StartsAt >= filter.From)
            && (!filter.To.HasValue || x.StartsAt <= filter.To)
            && (!filter.LocalId.HasValue || x.LocalId == filter.LocalId)
            && (!filter.TeamId.HasValue || x.TeamIds.Contains(filter.TeamId.Value))
            && (!filter.ServiceId.HasValue || x.ServiceIds.Contains(filter.ServiceId.Value))
            && (filter.OperationalStatus is null || x.OperationalStatus == filter.OperationalStatus)
            && (filter.ReviewStatus is null || x.ReviewStatus == filter.ReviewStatus)).ToArray();
        var pendencies = data.DocumentPendencies.Where(x => (!filter.LocalId.HasValue || x.LocalId == filter.LocalId)
            && (!filter.TeamId.HasValue || x.TeamIds.Contains(filter.TeamId.Value))
            && (!filter.ServiceId.HasValue || x.ServiceIds.Contains(filter.ServiceId.Value))
            && (!filter.From.HasValue || x.OccurredAt >= filter.From)
            && (!filter.To.HasValue || x.OccurredAt <= filter.To)
            && (filter.OperationalStatus is null || x.OperationalStatus == filter.OperationalStatus)).ToArray();
        var reviews = data.ReviewPendencies.Where(x => (!filter.LocalId.HasValue || x.LocalId == filter.LocalId)
            && (!filter.TeamId.HasValue || x.TeamIds.Contains(filter.TeamId.Value))
            && (filter.ServiceId is null || WorkPermitServiceCatalogEntry.All.Any(s => s.Id == filter.ServiceId && x.ServiceCodes.Contains(s.Code)))
            && (!filter.From.HasValue || x.CompletedAt >= filter.From)
            && (!filter.To.HasValue || x.CompletedAt <= filter.To)
            && (filter.ReviewStatus is null || filter.ReviewStatus == "AGUARDANDO_REVISAO")).ToArray();

        return new(appointments, pendencies, reviews);
    }

    private sealed record FilteredData(IReadOnlyList<DashboardAppointment> Appointments,
        IReadOnlyList<DashboardDocumentPendency> DocumentPendencies,
        IReadOnlyList<DashboardReviewPendency> Reviews);
}
