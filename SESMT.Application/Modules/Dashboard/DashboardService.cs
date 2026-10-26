using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Application.Modules.WorkPermits.Services;

namespace SESMT.Application.Modules.Dashboard;

public sealed class DashboardService(IDashboardRepository repository)
{
    private const int MaxActivityEvolutionMonths = 36;

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

    public async Task<DashboardActivityEvolution> GetActivityEvolutionAsync(Guid actorId, DashboardFilter filter, CancellationToken ct)
    {
        if (filter.OperationalStatus is not null || filter.ReviewStatus is not null)
            throw new ArgumentException("Filtros de situação não se aplicam à série histórica.");
        var now = DateTimeOffset.UtcNow;
        var end = filter.To ?? now;
        var start = filter.From ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
            throw new ArgumentException("from e to devem estar em UTC.");
        if (start > end)
            throw new ArgumentException("O início do período deve ser anterior ao fim.");
        var fromMonth = new DateOnly(start.Year, start.Month, 1);
        var toMonth = new DateOnly(end.Year, end.Month, 1);
        var monthCount = (toMonth.Year - fromMonth.Year) * 12 + toMonth.Month - fromMonth.Month + 1;
        if (monthCount > MaxActivityEvolutionMonths)
            throw new ArgumentException($"O período máximo é de {MaxActivityEvolutionMonths} meses.");

        if (filter.LocalId == Guid.Empty || filter.TeamId == Guid.Empty || filter.ServiceId == Guid.Empty)
            throw new ArgumentException("Os filtros de identificador devem conter UUIDs válidos.");
        var boundedFilter = filter with { From = start, To = end };
        var data = await repository.GetDataAsync(actorId, ct, boundedFilter);
        if ((filter.LocalId.HasValue && !data.AllowedLocalIds.Contains(filter.LocalId.Value))
            || (filter.TeamId.HasValue && !data.AllowedTeamIds.Contains(filter.TeamId.Value))
            || (filter.ServiceId.HasValue && !data.AllowedServiceIds.Contains(filter.ServiceId.Value)))
            throw new WorkPermitForbiddenException();

        var monthly = Enumerable.Range(0, monthCount).ToDictionary(
            offset => fromMonth.AddMonths(offset).ToString("yyyy-MM"),
            _ => new ActivityCounts());

        foreach (var transition in data.ActivityTransitions)
        {
            if (transition.OccurredAt < start || transition.OccurredAt > end
                || (filter.LocalId.HasValue && transition.LocalId != filter.LocalId)
                || (filter.TeamId.HasValue && !transition.TeamIds.Contains(filter.TeamId.Value))
                || (filter.ServiceId.HasValue && !transition.ServiceIds.Contains(filter.ServiceId.Value)))
                continue;

            var month = transition.OccurredAt.ToUniversalTime().ToString("yyyy-MM");
            if (!monthly.TryGetValue(month, out var counts)) continue;
            switch (transition.NewStatus)
            {
                case "CONCLUIDA": counts.Concluidas++; break;
                case "EM_EXECUCAO":
                case "SUSPENSA": counts.EmAndamento++; break;
                case "EM_VERIFICACAO":
                case "LIBERADA":
                case "DEVOLVIDA": counts.Pendentes++; break;
            }
        }

        var series = monthly.Select(entry => new DashboardActivityMonth(
            entry.Key, entry.Value.Concluidas, entry.Value.EmAndamento, entry.Value.Pendentes)).ToArray();
        return new(new(fromMonth.ToString("yyyy-MM"), toMonth.ToString("yyyy-MM")), series);
    }

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
        ValidateFilterScope(filter, data);
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

        return new(appointments, pendencies, reviews, data.ActivityTransitions);
    }

    private static void ValidateFilterScope(DashboardFilter filter, DashboardData data)
    {
        if ((filter.LocalId.HasValue && !data.AllowedLocalIds.Contains(filter.LocalId.Value))
            || (filter.TeamId.HasValue && !data.AllowedTeamIds.Contains(filter.TeamId.Value))
            || (filter.ServiceId.HasValue && !data.AllowedServiceIds.Contains(filter.ServiceId.Value)))
            throw new WorkPermitForbiddenException();
    }

    private sealed record FilteredData(IReadOnlyList<DashboardAppointment> Appointments,
        IReadOnlyList<DashboardDocumentPendency> DocumentPendencies,
        IReadOnlyList<DashboardReviewPendency> Reviews,
        IReadOnlyList<DashboardActivityTransition> ActivityTransitions);

    private sealed class ActivityCounts
    {
        public int Concluidas { get; set; }
        public int EmAndamento { get; set; }
        public int Pendentes { get; set; }
    }
}
