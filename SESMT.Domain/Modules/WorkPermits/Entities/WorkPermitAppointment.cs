using System.Text.Json;
using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Exceptions;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed record AppointmentState(string Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? ResponsibleId, Guid? TeamId, string[] ServiceCodes, Guid[] ParticipantIds);
public sealed record AppointmentEvent(string Action, int Version, Guid? ActorId,
    DateTimeOffset OccurredAt, string? Reason, AppointmentState? Before, AppointmentState After);

public sealed class WorkPermitAppointment : AggregateRootBase
{
    public const string Requested = "REQUESTED", Confirmed = "CONFIRMED", Cancelled = "CANCELLED";
    public Guid CompanyId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string Title { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public Guid? RequestedBy { get; private set; }
    public Guid? RequestedViaInviteId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? ResponsibleId { get; private set; }
    public string[] ServiceCodes { get; private set; } = [];
    public Guid[] ParticipantIds { get; private set; } = [];
    public string Status { get; private set; } = Requested;
    public int Version { get; private set; } = 1;
    public string HistoryJson { get; private set; } = "[]";

    private WorkPermitAppointment() { }

    public WorkPermitAppointment(Guid companyId, string title, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid requestedBy, Guid? locationId = null)
        : this(companyId, title, startsAt, endsAt, (Guid?)requestedBy, null, locationId) { }

    private WorkPermitAppointment(Guid companyId, string title, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? requestedBy, Guid? inviteId, Guid? locationId)
    {
        if (companyId == Guid.Empty || requestedBy == Guid.Empty || inviteId == Guid.Empty || requestedBy is null && inviteId is null)
            throw new ArgumentException("Empresa e origem da solicitação são obrigatórias.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("O título deve ter de 1 a 200 caracteres.");
        ValidatePeriod(startsAt, endsAt);

        CompanyId = companyId;
        LocationId = locationId;
        Title = title.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        RequestedBy = requestedBy;
        RequestedViaInviteId = inviteId;
        CreatedBy = requestedBy;
        Append("REQUESTED", requestedBy, null, null);
    }

    public static WorkPermitAppointment CreateRequest(Guid companyId, string title, DateTimeOffset startsAt,
        DateTimeOffset endsAt, Guid actorId, Guid locationId, Guid teamId, string[] services, Guid[] participants)
    {
        var appointment = new WorkPermitAppointment(companyId, title, startsAt, endsAt, actorId, locationId);
        appointment.SetInitialContext(teamId, services, participants);
        return appointment;
    }

    public static WorkPermitAppointment CreateFromInvite(Guid companyId, string title, DateTimeOffset startsAt,
        DateTimeOffset endsAt, Guid inviteId, Guid locationId, Guid teamId, string[] services, Guid[] participants)
    {
        var appointment = new WorkPermitAppointment(companyId, title, startsAt, endsAt, null, inviteId, locationId);
        appointment.SetInitialContext(teamId, services, participants);
        return appointment;
    }

    private void SetInitialContext(Guid teamId, string[] services, Guid[] participants)
    {
        ValidateContext(teamId, services, participants);
        TeamId = teamId; ServiceCodes = services.Order().ToArray(); ParticipantIds = participants.Order().ToArray();
        HistoryJson = "[]"; Append("REQUESTED", RequestedBy, null, null);
    }

    public void ConfigureContext(Guid teamId, string[] services, Guid[] participants, Guid actorId, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != Requested) throw new DomainException("Somente uma solicitação pendente pode mudar de contexto.");
        ValidateContext(teamId, services, participants);
        var before = Snapshot(); TeamId = teamId; ServiceCodes = services.Order().ToArray(); ParticipantIds = participants.Order().ToArray();
        Version++; MarkAsUpdated(actorId); Append("CONTEXT_CHANGED", actorId, before, null);
    }

    public void Confirm(Guid actorId, Guid responsibleId, int expectedVersion)
    {
        if (Status == Confirmed && ResponsibleId == responsibleId && Version == expectedVersion + 1) return;
        EnsureVersion(expectedVersion);
        if (Status != Requested) throw new DomainException("O agendamento não está aguardando confirmação.");
        if (actorId == Guid.Empty || responsibleId == Guid.Empty) throw new ArgumentException("Responsável e ator são obrigatórios.");
        var before = Snapshot(); ResponsibleId = responsibleId; Status = Confirmed;
        Version++; MarkAsUpdated(actorId); Append("CONFIRMED", actorId, before, null);
    }

    public void Reschedule(DateTimeOffset startsAt, DateTimeOffset endsAt, string reason, Guid actorId, int expectedVersion)
    {
        EnsureVersion(expectedVersion); ValidatePeriod(startsAt, endsAt); ValidateReason(reason);
        if (Status == Cancelled) throw new DomainException("Um agendamento cancelado não pode ser reagendado.");
        var before = Snapshot(); StartsAt = startsAt; EndsAt = endsAt;
        Version++; MarkAsUpdated(actorId); Append("RESCHEDULED", actorId, before, reason.Trim());
    }

    public void Cancel(string reason, Guid actorId, int expectedVersion)
    {
        ValidateReason(reason);
        if (Status == Cancelled && Version == expectedVersion + 1 && ReadHistory().LastOrDefault()?.Reason == reason.Trim()) return;
        EnsureVersion(expectedVersion);
        if (Status == Cancelled) throw new DomainException("Agendamento já cancelado.");
        var before = Snapshot(); Status = Cancelled;
        Version++; MarkAsUpdated(actorId); Append("CANCELLED", actorId, before, reason.Trim());
    }

    public void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion < 1 || Version != expectedVersion) throw new DomainException("O agendamento foi alterado. Recarregue a versão atual.");
    }
    public IReadOnlyList<AppointmentEvent> ReadHistory() => JsonSerializer.Deserialize<AppointmentEvent[]>(HistoryJson) ?? [];
    private AppointmentState Snapshot() => new(Status, StartsAt, EndsAt, ResponsibleId, TeamId, ServiceCodes.ToArray(), ParticipantIds.ToArray());
    private void Append(string action, Guid? actor, AppointmentState? before, string? reason)
    {
        var events = ReadHistory().ToList(); events.Add(new(action, Version, actor, DateTimeOffset.UtcNow, reason, before, Snapshot()));
        HistoryJson = JsonSerializer.Serialize(events);
    }
    private static void ValidatePeriod(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (startsAt.Offset != TimeSpan.Zero || endsAt.Offset != TimeSpan.Zero || endsAt <= startsAt)
            throw new ArgumentException("Informe início e término em UTC, com término posterior ao início.");
    }
    private static void ValidateContext(Guid teamId, string[] services, Guid[] participants)
    {
        if (teamId == Guid.Empty || services.Length == 0 || services.Distinct().Count() != services.Length
            || services.Any(code => !WorkPermitServiceCatalogEntry.All.Any(s => s.Code == code))
            || participants.Length == 0 || participants.Any(id => id == Guid.Empty) || participants.Distinct().Count() != participants.Length)
            throw new ArgumentException("Informe equipe, serviços e participantes válidos, sem duplicidade.");
    }
    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("Informe um motivo de até 1000 caracteres.");
    }
}
