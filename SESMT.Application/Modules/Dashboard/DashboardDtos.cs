namespace SESMT.Application.Modules.Dashboard;

public sealed record DashboardFilter(DateTimeOffset? From, DateTimeOffset? To, Guid? LocalId, Guid? TeamId,
    Guid? ServiceId, string? OperationalStatus, string? ReviewStatus);

public sealed record DashboardSummary(int EmVerificacao, int Liberadas, int EmExecucao,
    int ConcluidasAguardandoRevisao, int PendenciasDocumentais,
    DashboardPeriod Periodo);

public sealed record DashboardPeriod(DateTimeOffset? From, DateTimeOffset? To);
public sealed record DashboardAppointment(Guid Id, Guid? WorkPermitId, string Title, Guid CompanyId, Guid? LocalId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? OperationalStatus, string? ReviewStatus,
    IReadOnlyList<Guid> ServiceIds, IReadOnlyList<Guid> TeamIds);
public sealed record DashboardDocumentPendency(Guid WorkPermitId, Guid CompanyId, Guid? LocalId,
    string OperationalStatus, int PendingRequirements, DateTimeOffset OccurredAt, IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> TeamIds);
public sealed record DashboardReviewPendency(Guid WorkPermitId, Guid CompanyId, Guid? LocalId,
    IReadOnlyList<string> ServiceCodes, int Version, DateTimeOffset? CompletedAt, IReadOnlyList<Guid> TeamIds);
public sealed record DashboardActivityTransition(DateTimeOffset OccurredAt, string NewStatus, Guid? LocalId,
    IReadOnlyList<Guid> ServiceIds, IReadOnlyList<Guid> TeamIds);
public sealed record DashboardActivityMonth(string Mes, int Concluidas, int EmAndamento, int Pendentes);
public sealed record DashboardActivityEvolutionPeriod(string From, string To);
public sealed record DashboardActivityEvolution(DashboardActivityEvolutionPeriod Periodo,
    IReadOnlyList<DashboardActivityMonth> Series);
